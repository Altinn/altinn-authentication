using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.Models;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers.Oidc
{
    /// <summary>
    /// Front channel tests for <see cref="Authentication.Controllers.OidcFrontChannelController"/>.
    /// </summary>
    public class EndToEnd_OidcFrontChannelControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected IOidcServerClientRepository Repository => Services.GetRequiredService<IOidcServerClientRepository>();

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        private static readonly FakeTimeProvider _fakeTime = new(
    DateTimeOffset.Parse("2025-03-01T13:37:00Z")); // any stable baseline for tests

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IOidcProvider, Mocks.OidcProviderAdvancedMock>();
            
            // Make sure **all** app code that depends on TimeProvider gets this fake one
            services.AddSingleton<TimeProvider>(_fakeTime);

            string configPath = GetConfigPath();

            WebHostBuilder builder = new();
            builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");
            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
        }

        /// <summary>
        /// Scenario:
        /// User starts at RP (Arbeidsflate), gets redirected to /authorize endpoint.
        /// 1. /authorize persists downstream transaction and upstream transaction, then redirects to upstream provider.
        /// 2. User authenticates at upstream provider, which redirects back to /upstream/callback with code + state.
        /// 3. /upstream/callback redeems code for tokens at upstream provider, then creates downstream code and redirects to RP with code + original state.
        /// 4. RP redeems code for tokens at /token endpoint.
        /// 5. RP uses refresh_token at /token endpoint to get new tokens.
        /// 6. RP tries to reuse old refresh_token at /token endpoint and gets invalid_grant error.
        /// 7. Asserts on DB persistence after /authorize and /token.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Authorize_Persists_Downstream_And_Upstream_And_Redirects_Including_Callback_AndRedirectTo_DownStreamClient()
        {
            // Arrange
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1:  RP initiates the flow by redirecting user to /authorize endpoint. Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1));

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.UpstreamProviderCode)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = BuildTokenRequestForm(testScenario, create, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            var json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            OidcSession originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20));

            // ===== Phase 4: Refresh flow =====

            // 4.1 Assert we got a refresh_token back
            Assert.False(string.IsNullOrWhiteSpace(tokenResult.refresh_token));
            Assert.True(IsBase64Url(tokenResult.refresh_token!), "refresh_token must be base64url");
            string oldRefresh = tokenResult.refresh_token!;

            // 4.2 Use the refresh_token to get new tokens (client_secret_post like initial call)
            Dictionary<string, string> refreshForm = GetRefreshForm(testScenario, create, oldRefresh);

            using var refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;

            // 4.3 Basic assertions on rotated response
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());

            OidcSession refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);

            // Optional: scopes should be identical to original unless you down-scoped
            Assert.Equal(string.Join(' ', testScenario.Scopes), refreshed.scope);

            // 5 Is redirected to app 
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", appRedirectResponse.Headers.Location!.ToString());

            _fakeTime.Advance(TimeSpan.FromMinutes(5));

            // 6 Refresh call from app. In Apps the frontend Application do call against their 
            // own keepalive endpoint in app backend to get a new token. The backend in Apps do call against the /refresh endpoint in Authentication.
            // Code is here: https://github.com/Altinn/app-lib-dotnet/blob/main/src/Altinn.App.Api/Controllers/AuthenticationController.cs#L35
            HttpResponseMessage cookieRefreshResponse = await client.GetAsync(
                "/authentication/api/v1/refresh");

            string refreshToken = await cookieRefreshResponse.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken, testScenario, _fakeTime.GetUtcNow());

            // 4.4 Reuse detection: reusing old RT should fail with invalid_grant
            var reuseForm = GetRefreshForm(testScenario, create, oldRefresh);

            using var reuseResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(reuseForm));

            // Per spec: 400 invalid_grant
            Assert.Equal(HttpStatusCode.BadRequest, reuseResp.StatusCode);
            var reuseJson = await reuseResp.Content.ReadAsStringAsync();
            var reuseErr = JsonSerializer.Deserialize<Dictionary<string, string>>(reuseJson);
            Assert.Equal("invalid_grant", reuseErr!["error"]);

            // Helper local function for base64url validation
            static bool IsBase64Url(string s) =>
                s.All(c =>
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' || c == '_');

        }

        private async Task<(string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction)> AssertAutorizeRequestResult(OidcTestScenario testScenario, HttpResponseMessage authorizationRequestResponse)
        {
            OidcAssertHelper.AssertAuthorizeResponse(authorizationRequestResponse);

            string upstreamState = HttpUtility.ParseQueryString(authorizationRequestResponse.Headers.Location!.Query)["state"];

            // Asserting DB persistence after /authorize
            LoginTransaction loginTransaction = await OidcServerDatabaseUtil.GetDownstreamTransaction(testScenario.DownstreamClientId, testScenario.DownstreamState, DataSource);
            OidcAssertHelper.AssertLogingTransaction(loginTransaction, testScenario);

            UpstreamLoginTransaction createdUpstreamLogingTransaction = await OidcServerDatabaseUtil.GetUpstreamtransactrion(loginTransaction.RequestId, DataSource);
            OidcAssertHelper.AssertUpstreamLogingTransaction(createdUpstreamLogingTransaction, testScenario);
            return (upstreamState, createdUpstreamLogingTransaction);
        }

        private static Dictionary<string, string> GetRefreshForm(OidcTestScenario testScenario, OidcClientCreate create, string oldRefresh)
        {
            var refreshForm = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = oldRefresh,
                ["client_id"] = create.ClientId,
                ["client_secret"] = testScenario.ClientSecret // assuming your test client has this
            };
            return refreshForm;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private static OidcClientCreate NewClientCreate(OidcTestScenario testScenario) =>
            new()
            {
                ClientId = testScenario.DownstreamClientId,
                ClientName = "Test Client",
                ClientType = ClientType.Confidential,
                TokenEndpointAuthMethod = TokenEndpointAuthMethod.ClientSecretBasic,
                RedirectUris = testScenario.RedirectUris,
                AllowedScopes = testScenario.AllowedScopes,
                ClientSecretHash = testScenario.HashedClientSecret,
                ClientSecretExpiresAt = null,
                SecretRotationAt = null,
                JwksUri = null,
                JwksJson = null
            };

        private void ConfigureMockProviderTokenResponse(OidcTestScenario testScenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            Guid upstreamSID = Guid.NewGuid();
            OidcCodeResponse oidcCodeResponse = IdPortenTestTokenUtil.GetIdPortenTokenResponse(
                testScenario.Ssn, 
                createdUpstreamLogingTransaction.Nonce, 
                upstreamSID.ToString(), 
                createdUpstreamLogingTransaction.AcrValues, 
                testScenario.Amr?.ToArray(),
                createdUpstreamLogingTransaction.UpstreamClientId, 
                createdUpstreamLogingTransaction.Scopes,
                authTime);

            Mocks.OidcProviderAdvancedMock mock = Assert.IsType<Mocks.OidcProviderAdvancedMock>(
                Services.GetRequiredService<IOidcProvider>());
            var idpAuthCode = testScenario.UpstreamProviderCode; // what we will pass on callback

            mock.SetupSuccess(
                authorizationCode: idpAuthCode,
                clientId: createdUpstreamLogingTransaction.UpstreamClientId,
                redirectUri: createdUpstreamLogingTransaction.UpstreamRedirectUri.ToString(),
                codeVerifier: createdUpstreamLogingTransaction.CodeVerifier,
                response: oidcCodeResponse);
        }

        private HttpClient CreateClientWithHeaders()
        {
            var client = CreateClient();

            // Headers used by the controller to capture IP/UA/correlation
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AltinnTestClient/1.0");
            client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42"); // Test IP
            return client;
        }

        private static Dictionary<string, string> BuildTokenRequestForm(OidcTestScenario testScenario, OidcClientCreate create, string code)
        {
            Dictionary<string, string> tokenForm = new()
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = testScenario.DownstreamClientCallbackUrl,
                ["client_id"] = testScenario.DownstreamClientId,
                ["client_secret"] = testScenario.ClientSecret!,
                ["code_verifier"] = testScenario.DownstreamCodeVerifier,
            };
            return tokenForm;
        }
    }
}
