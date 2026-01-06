#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Clients.Interfaces;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.Models;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Altinn.Register.Contracts.V1;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers.Oidc
{
    /// <summary>
    /// Front channel tests for <see cref="Authentication.Controllers.OidcFrontChannelController"/>.
    /// In this test config is forced to use OIDC with an upstream provider
    /// </summary>
    public class EndToEndForceOidc_OidcFrontChannelControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected IOidcServerClientRepository Repository => Services.GetRequiredService<IOidcServerClientRepository>();

        protected IOidcSessionRepository SessionRepository => Services.GetRequiredService<IOidcSessionRepository>();

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        private readonly Mock<ISblCookieDecryptionService> _cookieDecryptionService = new();
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<IOidcDownstreamLogout> _downstreamLogoutClient = new();

        private FakeTimeProvider _fakeTime = null!;

        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Configure DI for tests, replacing external dependencies with fakes/mocks.
        /// </summary>
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            _fakeTime = new(DateTimeOffset.Parse("2025-03-01T08:00:00Z")); // any stable baseline for tests
            
            services.AddSingleton<IOidcProvider, Mocks.OidcProviderAdvancedMock>();
            
            // Make sure **all** app code that depends on TimeProvider gets this fake one
            services.AddSingleton<TimeProvider>(_fakeTime);

            string configPath = GetConfigPath();

            WebHostBuilder builder = new();
            builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .Build();

            _downstreamLogoutClient.Setup(q => q.TryLogout(It.IsAny<OidcClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
    .ReturnsAsync(true);
            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");
            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IProfile, ProfileFileMock>();
            services.AddSingleton<ISblCookieDecryptionService>(_cookieDecryptionService.Object);
            services.AddSingleton<IUserProfileService>(_userProfileService.Object);
            services.AddSingleton<IOidcDownstreamLogout>(_downstreamLogoutClient.Object);

            services.PostConfigure<GeneralSettings>(o =>
            {
                o.ForceOidc = true;   // “true” group
                o.EnableOidc = true;  // must be true for ForceOidc to have effect
                o.AuthorizationServerEnabled = true; // must be true for OIDC to work
            });
        }

        /// <summary>
        /// End-to-end happy path for the OIDC front-channel flow across Altinn components,
        /// including code exchange, refresh rotation, cross-app keep-alive, refresh expiry,
        /// re-authorization using an existing OP session, and federated logout orchestration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Time is anchored with <c>FakeTimeProvider</c> at <c>2025-03-01T08:00:00Z</c>.
        /// The test simulates a full user day starting in Arbeidsflate (RP), moving into
        /// Altinn Apps, and ending with logout (including upstream IdP involvement).
        /// </para>
        /// <list type="number">
        ///   <item><description>
        ///     <b>/authorize</b>: RP redirects to OP; OP persists <c>login_transaction</c> and
        ///     <c>upstream_login_transaction</c>, then 302s to the upstream IdP.
        ///   </description></item>
        ///   <item><description>
        ///     <b>/upstream/callback</b>: OP redeems upstream code, creates an OP session (<c>sid</c>),
        ///     issues a downstream authorization code, and redirects the browser back to RP with
        ///     <c>code</c> and original <c>state</c>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>/token (authorization_code)</b>: RP exchanges the code; assertions verify token
        ///     payloads and that the OP session exists in DB with the expected <c>sid</c>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>/token (refresh_token)</b>: RP rotates tokens; session sliding/consistency is asserted.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Altinn Apps keep-alive</b>: User is redirected to an app; repeated <b>/refresh</b>
        ///     returns cookie-based access tokens to keep the app session alive.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Refresh expiry</b>: After ~50 minutes of activity in a second app, attempting to reuse
        ///     the earlier refresh token yields <c>invalid_grant</c>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Re-authorize with OP session</b>: RP calls <b>/authorize</b> again; OP detects an
        ///     active runtime cookie/session and short-circuits to issue a new code without IdP login.
        ///     The resulting tokens map to the same <c>sid</c>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Logout</b>: RP triggers logout at OP (which federates to the upstream IdP).
        ///     The OP session row is deleted. The test then simulates the upstream IdP’s
        ///     front-channel logout callback to OP, which responds <c>200 OK</c>.
        ///   </description></item>
        /// </list>
        /// <para>
        /// Assertions cover HTTP status/redirects, DB persistence of transactions and session,
        /// token contents (including <c>sid</c> and scopes), refresh rotation behavior,
        /// refresh expiry (<c>invalid_grant</c>), and logout side effects (session removal,
        /// upstream front-channel acknowledgment).
        /// </para>
        /// </remarks>
        [Fact]
        public async Task TC1_Auth_Aa_App_Af_App_Af_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string tokenString = await tokenResp.Content.ReadAsStringAsync();

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20)); // 08:21

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(scope, refreshed.scope);
            }

            // ====== Phase 5: Redirect to App in Altinn Apps ======
            // User select a instance in Arbeidsflate and will be redirected to Altinn Apps to work on the instance. 
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", appRedirectResponse.Headers.Location!.ToString());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:26

            // ===== Phase 6: Keep alive flow from App =====
            // In Apps the frontend Application do call against their 
            // own keepalive endpoint in app backend to get a new token. The backend in Apps do call against the /refresh endpoint in Authentication.
            // Code is here: https://github.com/Altinn/app-lib-dotnet/blob/main/src/Altinn.App.Api/Controllers/AuthenticationController.cs#L35
            HttpResponseMessage cookieRefreshResponse = await client.GetAsync(
                "/authentication/api/v1/refresh");

            string refreshToken = await cookieRefreshResponse.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:31

            // Second keep alive from App
            HttpResponseMessage cookieRefreshResponse2 = await client.GetAsync(
               "/authentication/api/v1/refresh");

            string refreshToken2 = await cookieRefreshResponse2.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken2, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:36

            // ===== Phase 7: Try to reuse old but still active refresh_token =====
            // User returns to arbeisflate and do a new refresh. Arbeidsflate has still active session and do a refresh.
            Dictionary<string, string> refreshFormAfterAppVisit = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshed.refresh_token);

            using var refreshRespAfterAppVisit = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshFormAfterAppVisit));

            Assert.Equal(HttpStatusCode.OK, refreshRespAfterAppVisit.StatusCode);

            string refreshJsonAfterAppVisit = await refreshRespAfterAppVisit.Content.ReadAsStringAsync();
            TokenResponseDto refreshedAfterAppVisit = JsonSerializer.Deserialize<TokenResponseDto>(refreshJsonAfterAppVisit)!;

            TokenAssertsHelper.AssertTokenRefreshResponse(refreshedAfterAppVisit, testScenario, _fakeTime.GetUtcNow());

            // === Phase 8: User redirects to another app in Altinn Apps and stays there for 50 minutes.
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            // ==== Phase 9: Keep alive flow from second App in Altinn Apps =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21
                
                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 10: Returns to Arbeidsflate  =====
            // The refresh token should now be expired (max lifetime is 30 minutes and the token was issued at 08:01).
            // Arbeidsflate needs to redirect user to /authorize again to get a new code/refresh_token.
            Dictionary<string, string> reuseForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshedAfterAppVisit.refresh_token);

            using var reuseResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(reuseForm));

            Assert.Equal(HttpStatusCode.BadRequest, reuseResp.StatusCode);
            string reuseJson = await reuseResp.Content.ReadAsStringAsync();
            var reuseErr = JsonSerializer.Deserialize<Dictionary<string, string>>(reuseJson);
            Assert.Equal("invalid_grant", reuseErr!["error"]);

            testScenario.SetLoginAttempt(2); // Set next login attempt to get new state/nonce and pkce values

            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code2 = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm2 = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code2);

            using var tokenResp2 = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm2));

            Assert.Equal(HttpStatusCode.OK, tokenResp2.StatusCode);
            var json2 = await tokenResp2.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult2 = JsonSerializer.Deserialize<TokenResponseDto>(json2);

            // Asserts on token response structure
            string sid2 = TokenAssertsHelper.AssertTokenResponse(tokenResult2, testScenario, _fakeTime.GetUtcNow());

            Assert.Equal(sid, sid2); // should be same session as before

            // ===== Phase 11: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====
           
            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();
        
            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout?client_id=345345s&post_logout_redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi%2fv1%2flogout%2fhandleloggedout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            _downstreamLogoutClient.Verify(
            q => q.TryLogout(
            It.IsAny<OidcClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
        }

        /// <summary>
        /// Verify that downstream is logged out when upstream trigger logout.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TC1B_Auth_Aa_App_External_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string tokenString = await tokenResp.Content.ReadAsStringAsync();

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20)); // 08:21

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(scope, refreshed.scope);
            }

            // ====== Phase 5: Redirect to App in Altinn Apps ======
            // User select a instance in Arbeidsflate and will be redirected to Altinn Apps to work on the instance. 
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", appRedirectResponse.Headers.Location!.ToString());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:26

            // ===== Phase 6: Keep alive flow from App =====
            // In Apps the frontend Application do call against their 
            // own keepalive endpoint in app backend to get a new token. The backend in Apps do call against the /refresh endpoint in Authentication.
            // Code is here: https://github.com/Altinn/app-lib-dotnet/blob/main/src/Altinn.App.Api/Controllers/AuthenticationController.cs#L35
            HttpResponseMessage cookieRefreshResponse = await client.GetAsync(
                "/authentication/api/v1/refresh");

            string refreshToken = await cookieRefreshResponse.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:31

            // Second keep alive from App
            HttpResponseMessage cookieRefreshResponse2 = await client.GetAsync(
               "/authentication/api/v1/refresh");

            string refreshToken2 = await cookieRefreshResponse2.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken2, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:36

            // ===== Phase 7: Try to reuse old but still active refresh_token =====
            // User returns to arbeisflate and do a new refresh. Arbeidsflate has still active session and do a refresh.
            Dictionary<string, string> refreshFormAfterAppVisit = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshed.refresh_token);

            using var refreshRespAfterAppVisit = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshFormAfterAppVisit));

            Assert.Equal(HttpStatusCode.OK, refreshRespAfterAppVisit.StatusCode);

            string refreshJsonAfterAppVisit = await refreshRespAfterAppVisit.Content.ReadAsStringAsync();
            TokenResponseDto refreshedAfterAppVisit = JsonSerializer.Deserialize<TokenResponseDto>(refreshJsonAfterAppVisit)!;

            TokenAssertsHelper.AssertTokenRefreshResponse(refreshedAfterAppVisit, testScenario, _fakeTime.GetUtcNow());

            // === Phase 8: User redirects to another app in Altinn Apps and stays there for 50 minutes.
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            // ==== Phase 9: Keep alive flow from second App in Altinn Apps =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 10: Returns to Arbeidsflate  =====
            // The refresh token should now be expired (max lifetime is 30 minutes and the token was issued at 08:01).
            // Arbeidsflate needs to redirect user to /authorize again to get a new code/refresh_token.
            Dictionary<string, string> reuseForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshedAfterAppVisit.refresh_token);

            using var reuseResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(reuseForm));

            Assert.Equal(HttpStatusCode.BadRequest, reuseResp.StatusCode);
            string reuseJson = await reuseResp.Content.ReadAsStringAsync();
            var reuseErr = JsonSerializer.Deserialize<Dictionary<string, string>>(reuseJson);
            Assert.Equal("invalid_grant", reuseErr!["error"]);

            testScenario.SetLoginAttempt(2); // Set next login attempt to get new state/nonce and pkce values

            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code2 = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm2 = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code2);

            using var tokenResp2 = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm2));

            Assert.Equal(HttpStatusCode.OK, tokenResp2.StatusCode);
            var json2 = await tokenResp2.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult2 = JsonSerializer.Deserialize<TokenResponseDto>(json2);

            // Asserts on token response structure
            string sid2 = TokenAssertsHelper.AssertTokenResponse(tokenResult2, testScenario, _fakeTime.GetUtcNow());

            Assert.Equal(sid, sid2); // should be same session as before

            // ===== Phase 11: User navigates to external portal and after some time log out. Expect ID porten to call front channel logout === 

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);
            
            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            _downstreamLogoutClient.Verify(
            q => q.TryLogout(
            It.IsAny<OidcClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
        }

        /// <summary>
        /// Validates the full end-to-end happy path of the OIDC front-channel flow across Altinn components.
        /// 
        /// This test simulates a complete user journey starting from sign-in in Altinn Arbeidsflate (Relying Party)
        /// through the Altinn Authentication OIDC Provider and an upstream IdP (e.g., ID-porten), and back again.
        /// It verifies correct persistence and lifecycle handling of login transactions, token exchanges, and session
        /// management, including refresh tokens and application redirection.
        /// 
        /// The scenario covers:
        /// 1. User login initiated by Arbeidsflate via the /authorize endpoint.
        /// 2. Upstream provider callback to Altinn Authentication with authorization code.
        /// 3. Token issuance and verification of access, ID, and refresh tokens.
        /// 4. Long-lived session with multiple refresh cycles over simulated time.
        /// 5. Redirect to an Altinn App without valid cookie but with active Authentication session.
        /// 6. Successful token refresh after app visit.
        /// 7. User logout in Arbeidsflate, ensuring proper session invalidation and propagation to upstream provider,
        ///    including successful handling of the upstream front-channel logout request.
        /// 
        /// The test ensures correct state transitions, token structures, session persistence, and cleanup behavior
        /// in a normal (non-error) OIDC flow across the complete Altinn authentication architecture.
        /// </summary>
        [Fact]
        public async Task TC2_Auth_Aa_App_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 4: Working in Arbeidsflate =====
            // The user is active in Arbeidsflate for a long time. The session is kept Alive alive by doing refreshes.
            for (int i = 0; i < 20; i++)
            {
                Dictionary<string, string> refreshFormLoop = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
                using HttpResponseMessage refreshRespLoop = await client.PostAsync(
                    "/authentication/api/v1/token",
                    new FormUrlEncodedContent(refreshFormLoop));
                Assert.Equal(HttpStatusCode.OK, refreshRespLoop.StatusCode);
                string refreshJsonLoop = await refreshRespLoop.Content.ReadAsStringAsync();
                tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(refreshJsonLoop)!;
                TokenAssertsHelper.AssertTokenRefreshResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
                OidcSession? refreshedSessionLoop = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
                foreach (string scope in testScenario.Scopes)
                {
                    Assert.Contains(scope, tokenResult.scope);
                }

                _fakeTime.Advance(TimeSpan.FromMinutes(5));
            }

            // ====== Phase 5: Redirect to App in Altinn Apps ======
            // User select a instance in Arbeidsflate and will be redirected to Altinn Apps to work on the instance. There is no valid cooke, but should still work since the user have a valid session in Authentication.
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", appRedirectResponse.Headers.Location!.ToString());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:26

            // ===== Phase 6: Try to reuse old but still active refresh_token =====
            // User returns to arbeisflate and do a new refresh. Arbeidsflate has still active session and do a refresh.
            Dictionary<string, string> refreshFormAfterAppVisit = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token);

            using var refreshRespAfterAppVisit = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshFormAfterAppVisit));

            Assert.Equal(HttpStatusCode.OK, refreshRespAfterAppVisit.StatusCode);

            string refreshJsonAfterAppVisit = await refreshRespAfterAppVisit.Content.ReadAsStringAsync();
            TokenResponseDto refreshedAfterAppVisit = JsonSerializer.Deserialize<TokenResponseDto>(refreshJsonAfterAppVisit)!;

            TokenAssertsHelper.AssertTokenRefreshResponse(refreshedAfterAppVisit, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 7: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout?client_id=345345s&post_logout_redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi%2fv1%2flogout%2fhandleloggedout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            _downstreamLogoutClient.Verify(
                q => q.TryLogout(
                It.IsAny<OidcClient>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()), 
                Times.Once);
        }

        /// <summary>
        /// End-to-end happy path where:
        /// 1) A user opens an Altinn App (not authenticated there) which redirects to the standard
        ///    authentication endpoint (with a <c>goto</c> URL). Altinn Authentication builds a PAR/JAR-style
        ///    upstream authorize request (incl. <c>state</c>, <c>nonce</c>, PKCE <c>code_challenge</c>) and
        ///    redirects to the upstream IdP.
        /// 2) After a successful upstream login, the upstream callback to Altinn Authentication is simulated;
        ///    Altinn Authenticaiton creates a session i database and creates a AltinnStudioRuntime cookie with JWT and redirects the browser back to the original app.
        /// 3) Over time, the app keeps the Altinn session alive by calling <c>/authentication/api/v1/refresh</c>
        ///    repeatedly; each response is validated as a usable cookie-based access token.
        /// 4) Later, the user visits Arbeidsflate. Because an Altinn session is active, the authorization request
        ///    short-circuits: Altinn immediately returns a new authorization code without re-authenticating upstream.
        ///    The test redeems that code at <c>/authentication/api/v1/token</c> and validates the token response,
        ///    extracting the current <c>sid</c>.
        /// 5) The user logs out from Arbeidsflate via <c>/authentication/api/v1/openid/logout</c>. The test verifies:
        ///    (a) browser is redirected to the upstream IdP’s logout endpoint,
        ///    (b) the Altinn OIDC session is removed from the database,
        ///    (c) a simulated upstream front-channel logout call to
        ///        <c>/authentication/api/v1/upstream/frontchannel-logout</c> returns <c>200 OK</c> with body <c>"OK"</c>.
        /// </summary>
        /// <remarks>
        /// - Verifies correct propagation and validation of OIDC artifacts (<c>state</c>, <c>nonce</c>, PKCE).
        /// - Asserts persistence and cleanup of <c>LoginTransaction</c>/<c>UpstreamLoginTransaction</c> and <c>OidcSession</c>.
        /// - Uses a fake clock to simulate elapsed time for refresh and later authorization.
        /// - Confirms redirect targets for both login and logout and validates issued tokens and session lifecycle.
        /// </remarks>
        [Fact]
        public async Task TC3_Auth_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://login.idporten.no/authorize?response_type=code&client_id=345345s&redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

            string location = app2RedirectResponse.Headers.Location!.ToString();

            string state = HttpUtility.ParseQueryString(new Uri(location).Query)["state"]!;
            string nonce = HttpUtility.ParseQueryString(new Uri(location).Query)["nonce"]!;
            string codeChallenge = HttpUtility.ParseQueryString(new Uri(location).Query)["code_challenge"]!;
            string redirectUri = HttpUtility.ParseQueryString(new Uri(location).Query)["redirect_uri"]!;
            Uri redirectUriParsed = new Uri(redirectUri!);
            Assert.NotNull(nonce);
            Assert.NotNull(state);
            Assert.NotNull(codeChallenge);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamState, UpstreamLoginTransaction? createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, app2RedirectResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"{redirectUriParsed.AbsolutePath}?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode()!)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", callbackResp.Headers.Location!.ToString());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout?client_id=345345s&post_logout_redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi%2fv1%2flogout%2fhandleloggedout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            _downstreamLogoutClient.Verify(
            q => q.TryLogout(
            It.IsAny<OidcClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
        }

        /// <summary>
        /// End-to-end scenario verifying authentication, session persistence, and logout 
        /// when the flow originates from an existing Altinn 2 (.ASPXAUTH) login.
        /// 
        /// The test covers:
        /// 1. Initial access to an Altinn App where the user is already authenticated via Altinn 2.
        ///    - Validates that the .ASPXAUTH cookie is accepted and converted into a valid Altinn OIDC session.
        ///    - Confirms redirect back to the application without invoking any upstream identity provider.
        /// 2. Repeated session refreshes through the /refresh endpoint, ensuring that the Altinn session 
        ///    remains active during extended usage.
        /// 3. Access to Arbeidsflate (RP) while a valid Altinn Runtime session exists, verifying 
        ///    that reauthentication is bypassed and the user is directly authorized.
        /// 4. Token issuance via /token endpoint and validation of session linkage and claims integrity.
        /// 5. Logout flow — verifying that logout triggers the correct redirect to the local Altinn 2 
        ///    logout endpoint and that the Altinn session is fully invalidated in the database.
        /// 
        /// Expected results:
        /// - Altinn 2 authentication cookie is successfully upgraded into an Altinn OIDC session.
        /// - Session refresh and reuse work seamlessly across applications.
        /// - Tokens are correctly bound to the authenticated Altinn session.
        /// - Logout removes all session data and redirects to Altinn 2 logout as expected.
        /// </summary>
        [Fact]
        public async Task TC4_Auth_A2_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders(new()
            {
                [".AspxAuth"] = "DummyAuthToken12345"
            });

            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");
            ConfigureSblTokenMockByScenario(testScenario);

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            // It already has a .ASPXauth that is valid and can be used to create a session in Authentication
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            OidcAssertHelper.AssertAuthorizedRedirect(app2RedirectResponse, testScenario, _fakeTime.GetUtcNow());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// Scenario where user is logged in in Altinn 2 and navigates to Arbeidsflate directly
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TC4B_Auth_A2_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders(new()
            {
                [".AspxAuth"] = "DummyAuthToken12345"
            });

            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");
            ConfigureSblTokenMockByScenario(testScenario);

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects to Arbeidsflate and is not authenticated. ======
            // It already has a .ASPXauth that is valid and can be used to create a session in Authentication
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn2 ticket and should get a direct back with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);
            OidcAssertHelper.AssertAuthorizedRedirect(authorizationRequestResponse2, testScenario, _fakeTime.GetUtcNow());

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// Same scenario as above with a Level 1 Arbeidsflate scenario.
        /// Needed for bruksmønster
        /// </summary>
        [Fact]
        public async Task TC4C_Auth_A2_L1_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders(new()
            {
                [".AspxAuth"] = "DummyAuthToken12345"
            });

            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_Level1");
            ConfigureSblTokenMockByScenario(testScenario);

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            // It already has a .ASPXauth that is valid and can be used to create a session in Authentication
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            OidcAssertHelper.AssertAuthorizedRedirect(app2RedirectResponse, testScenario, _fakeTime.GetUtcNow());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// End-to-end scenario verifying authentication, session management, and logout 
        /// when the flow starts with an existing Altinn 2 (.ASPXAUTH) login.
        /// 
        /// The test covers:
        /// 1. Initial access to an Altinn App where the user is already authenticated through Altinn 2.
        ///    - Validates that the .ASPXAUTH cookie is accepted and transformed into an Altinn OIDC session.
        ///    - Confirms redirect back to the original application without upstream provider interaction.
        /// 2. Continuous session refresh handling through the /refresh endpoint during active app usage.
        /// 3. Accessing Arbeidsflate (RP) while the Altinn Runtime session is active, verifying 
        ///    that a new authorization request reuses the existing authenticated session.
        /// 4. Token redemption for Arbeidsflate via /token endpoint and validation of token structure and claims.
        /// 5. Logout flow — verifying that logout correctly removes the Altinn OIDC session and 
        ///    redirects to the local Altinn 2 logout endpoint.
        /// 
        /// Expected results:
        /// - The Altinn 2 authentication cookie is seamlessly converted into a valid Altinn OIDC session.
        /// - Session refresh extends validity without reauthentication.
        /// - Tokens are correctly issued and bound to the Altinn session.
        /// - Logout invalidates the session and redirects to Altinn 2 logout, ensuring full cleanup.
        /// </summary>
        [Fact]
        public async Task TC5_Selfidentified_Auth_A2_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders(new()
            {
                [".AspxAuth"] = "DummyAuthToken12345"
            });

            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Selvidentifisert_Altinn2");
            ConfigureSblTokenMockByScenario(testScenario);

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            // It already has a .ASPXauth that is valid and can be used to create a session in Authentication
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            OidcAssertHelper.AssertAuthorizedRedirect(app2RedirectResponse, testScenario, _fakeTime.GetUtcNow());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;
            Assert.NotEmpty(code);

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// End-to-end scenario verifying authentication, session management, and logout for a UIDP user 
        /// with an existing profile in Altinn across multiple relying parties (Altinn App and Arbeidsflate).
        ///
        /// The test covers:
        /// 1. Initial authentication via UIDP when accessing an Altinn App.
        ///    - Validates redirect to the upstream provider and creation of login transactions.
        /// 2. Callback handling and redirect back to the original Altinn App after upstream login.
        /// 3. Ongoing session refreshes to ensure the user remains authenticated during extended app usage.
        /// 4. Accessing Arbeidsflate with an active Altinn Runtime session, verifying reuse of existing 
        ///    authentication without a new upstream login.
        /// 5. Token redemption and session verification for Arbeidsflate.
        /// 6. Logout flow — verifying coordinated logout propagation to both Altinn and the upstream UIDP provider.
        /// 
        /// Expected results:
        /// - The existing Altinn profile is correctly linked to the UIDP identity.
        /// - Session persistence and refresh behave as expected across multiple relying parties.
        /// - Logout triggers the upstream UIDP end session and fully removes the Altinn session from the database.
        /// </summary>
        [Fact]
        public async Task TC6_UIDPAuth_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Existing_UidpUser");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?iss=uidp-anonym&goto=https%3A%2F%2Fudir.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://uidp.udir.no/connect/authorize?response_type=code&client_id=sdfdsfeasfyhyy&redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

            string location = app2RedirectResponse.Headers.Location!.ToString();

            string state = HttpUtility.ParseQueryString(new Uri(location).Query)["state"]!;
            string nonce = HttpUtility.ParseQueryString(new Uri(location).Query)["nonce"]!;
            string codeChallenge = HttpUtility.ParseQueryString(new Uri(location).Query)["code_challenge"]!;
            Assert.NotNull(nonce);
            Assert.NotNull(state);
            Assert.NotNull(codeChallenge);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamState, UpstreamLoginTransaction? createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, app2RedirectResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockUidpProviderTokenResponseUidp(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());
            await ConfigureProfileMock(testScenario);

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode()!)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
            Assert.StartsWith("https://udir.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", callbackResp.Headers.Location!.ToString());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);
            Debug.Assert(tokenResult != null);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            
            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://uidp.udir.no/connect/endsession", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// End-to-end scenario verifying authentication, session handling, and logout for a UIDP user 
        /// with an existing profile in Altinn across multiple relying parties (Altinn App and Arbeidsflate).
        /// 
        /// The test covers:
        /// 1. Initial authentication via UIDP when accessing an Altinn App.
        ///    - Validates redirect to the upstream provider and creation of login transactions.
        /// 2. Successful callback and redirection back to the original Altinn App after upstream login.
        /// 3. Continuous session refreshes to keep the session alive over time.
        /// 4. Accessing Arbeidsflate with an existing Altinn Runtime session, verifying reuse of the active session 
        ///    without triggering a new upstream authentication.
        /// 5. Token redemption and session validation for Arbeidsflate.
        /// 6. Logout flow — ensuring coordinated logout from both Altinn and the upstream UIDP provider.
        /// 
        /// Expected results:
        /// - Login transactions and sessions are correctly persisted and reused between Altinn App and Arbeidsflate.
        /// - Refresh requests correctly extend session lifetime.
        /// - Logout triggers upstream UIDP logout and removes the Altinn session record from the database.
        /// </summary>
        [Fact]
        public async Task TC7_UIDPAuth_NewUser_App_Aa_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("New_UidpUser");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?iss=uidp-anonym&goto=https%3A%2F%2Fudir.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://uidp.udir.no/connect/authorize?response_type=code&client_id=sdfdsfeasfyhyy&redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

            string location = app2RedirectResponse.Headers.Location!.ToString();

            string state = HttpUtility.ParseQueryString(new Uri(location).Query)["state"]!;
            string nonce = HttpUtility.ParseQueryString(new Uri(location).Query)["nonce"]!;
            string codeChallenge = HttpUtility.ParseQueryString(new Uri(location).Query)["code_challenge"]!;
            string redirectUri = HttpUtility.ParseQueryString(new Uri(location).Query)["redirect_uri"]!;
            Uri redirectUriParsed = new Uri(redirectUri!);
            Assert.NotNull(nonce);
            Assert.NotNull(state);
            Assert.NotNull(codeChallenge);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamState, UpstreamLoginTransaction? createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, app2RedirectResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockUidpProviderTokenResponseUidp(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());
            await ConfigureProfileMock(testScenario);

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"{redirectUriParsed.AbsolutePath}?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode()!)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
            Assert.StartsWith("https://udir.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", callbackResp.Headers.Location!.ToString());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 4: Goes to Arbeidsflate  =====
            // The user does not have a valid session in Arbeidsflate. So user is redirected to the standard authorize endpoint with new state, nonce and pkce values.
            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            string json = await tokenResp.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult = JsonSerializer.Deserialize<TokenResponseDto>(json);

            // Asserts on token response structure
            string sidFromCodeResponse = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 5: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://uidp.udir.no/connect/endsession", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// End-to-end scenario verifying authentication level elevation (Level 3 → Level 4) 
        /// and complete session lifecycle for an Arbeidsflate (RP) user through the Altinn 
        /// Authentication OIDC Provider.
        /// 
        /// The test covers:
        /// 1. Initial authentication at Level 3 via the upstream provider.
        /// 2. Token issuance, session creation, and refresh flow validation.
        /// 3. Detection of insufficient authentication level when accessing a Level 4 resource.
        /// 4. Re-authentication (upgrade to Level 4) and issuance of a new session and tokens.
        /// 5. Logout flow — verifying both Altinn and upstream (ID-porten) logout propagation.
        /// 
        /// Expected results:
        /// - Tokens and sessions are correctly created, refreshed, upgraded, and invalidated.
        /// - Previous session is removed when elevating from Level 3 to Level 4.
        /// - Logout redirects to ID-porten and removes session records in Altinn DB.
        /// </summary>
        [Fact]
        public async Task TC8_Auth_Level3_Aa_Level_4_Af_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_Level34");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string tokenString = await tokenResp.Content.ReadAsStringAsync();

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20)); // 08:21

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(scope, refreshed.scope);
            }

            // ===== Phase 5: User has to low level to access a corresponence in arbeidsflate that requires level 4. User is redirected to /authorize again to elevate level =====
            // Update test scenario to require level4
            OidcSession? originalSessionBeforeUpgrade = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSessionBeforeUpgrade, testScenario, _fakeTime.GetUtcNow());

            // Update test scenario to require level4
            testScenario.Acr = ["idporten-loa-high"]; 
            testScenario.Amr = ["BankID Mobil"];
            testScenario.SetLoginAttempt(2);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string upgradeUrl = testScenario.GetAuthorizationRequestUrl();

            // Expected result is a redirect to upstream provider. Uers has a valid AltinnStudioRuntime cooke and code will verify if that is connectd to a session with to low level.
            HttpResponseMessage authorizationUpgradeRequestResponse = await client.GetAsync(upgradeUrl);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamUpgradeState, UpstreamLoginTransaction? createdUpstreamUpgradeLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationUpgradeRequestResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamUpgradeLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider. Now with BankID level 4
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamUpgradeLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 6: simulate provider redirecting back to Altinn with code + upstream state for the upgrade authentication request ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string upgradeCallbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamUpgradeState!)}";

            HttpResponseMessage upgradeCallbackResp = await client.GetAsync(upgradeCallbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(upgradeCallbackResp, testScenario, _fakeTime.GetUtcNow());
            string upgradeCode = HttpUtility.ParseQueryString(upgradeCallbackResp.Headers.Location!.Query)["code"]!;
            OidcSession? originalSessionAfterUpgrade = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(originalSessionAfterUpgrade);

            // === Phase 7: Downstream client redeems code for tokens for upgraded session ===
            Dictionary<string, string> tokenFormUpgrade = OidcServerTestUtils.BuildTokenRequestForm(testScenario, upgradeCode);

            using var upgradeTokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenFormUpgrade));

            Assert.Equal(HttpStatusCode.OK, upgradeTokenResp.StatusCode);
            TokenResponseDto? tokenResultUpgrade = await upgradeTokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string updateTokenString = await upgradeTokenResp.Content.ReadAsStringAsync();
            Debug.Assert(tokenResultUpgrade != null);

            // Asserts on token response structure
            string sidUpgraded = TokenAssertsHelper.AssertTokenResponse(tokenResultUpgrade, testScenario, _fakeTime.GetUtcNow());
            OidcSession? upgradedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidUpgraded, DataSource);
            OidcAssertHelper.AssertValidSession(upgradedSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(upgradedSession != null);

            // ===== Phase 8: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidUpgraded, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(upgradedSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(upgradedSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);
        }

        [Fact]
        public async Task TC9_Auth_App_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // === Phase 1: User redirects an app in Altinn Apps and is not authenticated. The app redirects to the standard authentication endpoint with goto URL
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://login.idporten.no/authorize?response_type=code&client_id=345345s&redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

            string location = app2RedirectResponse.Headers.Location!.ToString();

            string state = HttpUtility.ParseQueryString(new Uri(location).Query)["state"]!;
            string nonce = HttpUtility.ParseQueryString(new Uri(location).Query)["nonce"]!;
            string codeChallenge = HttpUtility.ParseQueryString(new Uri(location).Query)["code_challenge"]!;
            string redirectUri = HttpUtility.ParseQueryString(new Uri(location).Query)["redirect_uri"]!;
            Uri redirectUriParsed = new Uri(redirectUri!);
            Assert.NotNull(nonce);
            Assert.NotNull(state);
            Assert.NotNull(codeChallenge);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamState, UpstreamLoginTransaction? createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, app2RedirectResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"{redirectUriParsed.AbsolutePath}?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode()!)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", callbackResp.Headers.Location!.ToString());
            string sid = OidcAssertHelper.AssertCallbackResponseUnregistratedClient(callbackResp, testScenario, _fakeTime.GetUtcNow());

            // === Phase 3: Uses the app for an extensive periode. The app triggers refreshes to keep the session alive =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ===== Phase 4: User is done for the day. Press Logout in App. This should trigger the old logout endpoint configured in app. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/logout/");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);
        }

        [Fact]
        public async Task TC10_Auth_Consent_Logout_WithCookie_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);
           
            // === Phase 1: User request to open a Consent request. Consent page send user to authentication for login. Sends with additional 
            HttpResponseMessage app2RedirectResponse = await client.GetAsync("/authentication/api/v1/authentication?goto=https%3a%2f%2flocalhost%2faccessmanagement%2fui%2fconsent%2frequest%3fid%3d9383f24f-756e-4531-a341-652cff24e4f5%26DONTCHOOSEREPORTEE%3dtrue");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://login.idporten.no/authorize?response_type=code&client_id=345345s&redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

            string location = app2RedirectResponse.Headers.Location!.ToString();

            string state = HttpUtility.ParseQueryString(new Uri(location).Query)["state"]!;
            string nonce = HttpUtility.ParseQueryString(new Uri(location).Query)["nonce"]!;
            string codeChallenge = HttpUtility.ParseQueryString(new Uri(location).Query)["code_challenge"]!;
            string redirectUri = HttpUtility.ParseQueryString(new Uri(location).Query)["redirect_uri"]!;
            Uri redirectUriParsed = new Uri(redirectUri!);
            Assert.NotNull(nonce);
            Assert.NotNull(state);
            Assert.NotNull(codeChallenge);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string? upstreamState, UpstreamLoginTransaction? createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, app2RedirectResponse, _fakeTime.GetUtcNow());
            Debug.Assert(createdUpstreamLogingTransaction != null);

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"{redirectUriParsed.AbsolutePath}?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode()!)}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
            Assert.StartsWith("https://localhost/accessmanagement/ui/consent/request?id=", callbackResp.Headers.Location!.ToString());
            string sid = OidcAssertHelper.AssertCallbackResponseUnregistratedClient(callbackResp, testScenario, _fakeTime.GetUtcNow());

            // ===== Phase 2: User has consented. User logs out. Altinn Consent set a cookie to make user be redirected to the requested URL when =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/logout/");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            // Simulate that Altin Consent has set the AltinnLogoutInfo cookie with details how to handle 
            const string cookieValue = "amSafeRedirectUrl%3DAAEAABqMH6oghdPbHSLxY9KyclfoAc%2BNv9YWsTtcJ5DunicaXGtTPnXLvhZbOsWBxfrggTQBHKqET9nXwxyIZOXe7AtA5LJ2m5lZTwTnh9xwt3XK1AsU3cct2v4UWgcv0MQwmnfqPejePkSB0ZntTquSUeBH%2BnXiHHQVqnnhbNgr0k%2BwOk04Bvrysjfs5iez1oHoeaTi21eVa4M6Rf8JZet3nCB%2F8SvNE3s%2FGRxvZt1eka2pyTNC8Vh8YrH9mVK%2B%2BHPsd7%2F6oZ847s0MDdJs1haEXoQhjry3xS%2BUvrIXnYTzbJbGO5ALAj%2BRoTgWmWEKRKO9Xe3I7N34On5LrnOX%2BDWqVXQQAAAAQRFmHREBcLENfNwC5Y451mmb2hyhLS4p5cw2w2DXuikW22NKviEWLgdjWTPELIcknaEOojIO%2B6jXVcD%2F18GwV51c1AJBWi5Gioi0mtTi0eZZdUVX%2B1U%2FM0xelyRz6V8vvT%2BudOFcHLyRrD7XQlEHVfjbxbMpC7G%2BWa3t1FrIwdEl%2F4f%2Fb8RteQNxCtLuoBd0ILxcfVDoYNMkN%2Fbb3rJBKA%3D%3D";

            var req = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout/");
            req.Headers.Add("Cookie", $"AltinnLogoutInfo={cookieValue}");

            using var afterLogoute = await client.SendAsync(req);
            string handleloggedoutContent = await afterLogoute.Content.ReadAsStringAsync();

            Assert.Equal("https://am.ui.localhost/accessmanagement/api/v1/logoutredirect", afterLogoute.Headers.Location!.ToString());
        }

        [Fact]
        public async Task TC11_Auth_Aa_SessionEnd_App_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string tokenString = await tokenResp.Content.ReadAsStringAsync();

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20)); // 08:21

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(scope, refreshed.scope);
            }

            // == Phase 5: User logs out from external portal. Session in database is dead, but cookie is still available
            await SessionRepository.DeleteBySidAsync(sid);

            // ====== Phase 6: Redirect to App in Altinn Apps ======
            // User select a instance in Arbeidsflate and will be redirected to Altinn Apps to work on the instance. 
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://login.idporten.no", appRedirectResponse.Headers.Location!.ToString());
        }


        [Fact]
        public async Task TC12_Auth_IdPortenEmail_Aa_App_Af_App_Af_Logout_End_To_End_OK()
        {
            // Create HttpClient with default headers for IP, UA, correlation. 
            using HttpClient client = CreateClientWithHeaders();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert a client that matches the authorize request
            OidcClientCreate create = OidcServerTestUtils.NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            // 08:00:00 UTC — start of day: user signs in to Arbeidsflate (RP).
            // Arbeidsflate redirects the browser to the Altinn Authentication OIDC Provider’s /authorize endpoint.
            string url = testScenario.GetAuthorizationRequestUrl();

            // === Phase 1: RP (Relying Party) initiates the flow by redirecting user to /authorize endpoint.
            // Expected result is a redirect to upstream provider.
            HttpResponseMessage authorizationRequestResponse = await client.GetAsync(url);

            // Assert: Result of /authorize. Should be a redirect to upstream provider with code_challenge, state, etc. LoginTransaction should be persisted. UpstreamLoginTransaction should be persisted.
            (string upstreamState, UpstreamLoginTransaction createdUpstreamLogingTransaction) = await AssertAutorizeRequestResult(testScenario, authorizationRequestResponse, _fakeTime.GetUtcNow());

            // Assume it takes 1 minute for the user to authenticate at the upstream provider
            _fakeTime.Advance(TimeSpan.FromMinutes(1)); // 08:01

            // Configure the mock to return a successful token response for this exact callback. We need to know the exact code_challenge, client_id, redirect_uri, code_verifier to match.
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original client redirect_uri.
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}&state={Uri.EscapeDataString(upstreamState!)}";

            HttpResponseMessage callbackResp = await client.GetAsync(callbackUrl);

            // Should redirect to downstream client redirect_uri with ?code=...&state=original_downstream_state
            OidcAssertHelper.AssertCallbackResponse(callbackResp, testScenario, _fakeTime.GetUtcNow());
            string code = HttpUtility.ParseQueryString(callbackResp.Headers.Location!.Query)["code"]!;

            // === Phase 3: Downstream client redeems code for tokens ===
            Dictionary<string, string> tokenForm = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code);

            using var tokenResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm));

            Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
            TokenResponseDto? tokenResult = await tokenResp.Content.ReadFromJsonAsync<TokenResponseDto>(jsonSerializerOptions);
            string tokenString = await tokenResp.Content.ReadAsStringAsync();

            // Asserts on token response structure
            string sid = TokenAssertsHelper.AssertTokenResponse(tokenResult, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(tokenResult != null);

            OidcSession? originalSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(originalSession, testScenario, _fakeTime.GetUtcNow());

            // Advance time by 20 minutes (user is active in RP; we’ll now refresh)
            _fakeTime.Advance(TimeSpan.FromMinutes(20)); // 08:21

            // ===== Phase 4: Refresh flow =====
            // RP (Arbeidsflate) uses refresh_token to get new tokens. The user is still active in Arbeidsflate. Session is expanded
            Dictionary<string, string> refreshForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, tokenResult.refresh_token!);
            using HttpResponseMessage refreshResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshForm));

            Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
            string refreshJson = await refreshResp.Content.ReadAsStringAsync();
            TokenResponseDto refreshed = JsonSerializer.Deserialize<TokenResponseDto>(refreshJson)!;
            TokenAssertsHelper.AssertTokenRefreshResponse(refreshed, testScenario, _fakeTime.GetUtcNow());
            OidcSession? refreshedSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(scope, refreshed.scope);
            }

            // ====== Phase 5: Redirect to App in Altinn Apps ======
            // User select a instance in Arbeidsflate and will be redirected to Altinn Apps to work on the instance. 
            HttpResponseMessage appRedirectResponse = await client.GetAsync(
                "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, appRedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", appRedirectResponse.Headers.Location!.ToString());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:26

            // ===== Phase 6: Keep alive flow from App =====
            // In Apps the frontend Application do call against their 
            // own keepalive endpoint in app backend to get a new token. The backend in Apps do call against the /refresh endpoint in Authentication.
            // Code is here: https://github.com/Altinn/app-lib-dotnet/blob/main/src/Altinn.App.Api/Controllers/AuthenticationController.cs#L35
            HttpResponseMessage cookieRefreshResponse = await client.GetAsync(
                "/authentication/api/v1/refresh");

            string refreshToken = await cookieRefreshResponse.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:31

            // Second keep alive from App
            HttpResponseMessage cookieRefreshResponse2 = await client.GetAsync(
               "/authentication/api/v1/refresh");

            string refreshToken2 = await cookieRefreshResponse2.Content.ReadAsStringAsync();
            TokenAssertsHelper.AssertCookieAccessToken(refreshToken2, testScenario, _fakeTime.GetUtcNow());

            _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:36

            // ===== Phase 7: Try to reuse old but still active refresh_token =====
            // User returns to arbeisflate and do a new refresh. Arbeidsflate has still active session and do a refresh.
            Dictionary<string, string> refreshFormAfterAppVisit = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshed.refresh_token);

            using var refreshRespAfterAppVisit = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(refreshFormAfterAppVisit));

            Assert.Equal(HttpStatusCode.OK, refreshRespAfterAppVisit.StatusCode);

            string refreshJsonAfterAppVisit = await refreshRespAfterAppVisit.Content.ReadAsStringAsync();
            TokenResponseDto refreshedAfterAppVisit = JsonSerializer.Deserialize<TokenResponseDto>(refreshJsonAfterAppVisit)!;

            TokenAssertsHelper.AssertTokenRefreshResponse(refreshedAfterAppVisit, testScenario, _fakeTime.GetUtcNow());

            // === Phase 8: User redirects to another app in Altinn Apps and stays there for 50 minutes.
            HttpResponseMessage app2RedirectResponse = await client.GetAsync(
               "/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.localhost%2Ftad%2Fpagaendesak%3FDONTCHOOSEREPORTEE%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba");
            Assert.Equal(HttpStatusCode.Redirect, app2RedirectResponse.StatusCode);
            Assert.StartsWith("https://tad.apps.localhost/tad/pagaendesak?DONTCHOOSEREPORTEE=true#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba", app2RedirectResponse.Headers.Location!.ToString());

            // ==== Phase 9: Keep alive flow from second App in Altinn Apps =====
            for (int i = 0; i < 9; i++)
            {
                _fakeTime.Advance(TimeSpan.FromMinutes(5)); // 08:41 08:46 08:51 08:56 08:59 09:06 09:11 09:16 09:21

                // Second keep alive from App
                HttpResponseMessage cookieRefreshResponseFromSecondApp2 = await client.GetAsync(
                   "/authentication/api/v1/refresh");
                string refreshToken2FromSecondApp = await cookieRefreshResponseFromSecondApp2.Content.ReadAsStringAsync();
                TokenAssertsHelper.AssertCookieAccessToken(refreshToken2FromSecondApp, testScenario, _fakeTime.GetUtcNow());
            }

            // ==== Phase 10: Returns to Arbeidsflate  =====
            // The refresh token should now be expired (max lifetime is 30 minutes and the token was issued at 08:01).
            // Arbeidsflate needs to redirect user to /authorize again to get a new code/refresh_token.
            Dictionary<string, string> reuseForm = OidcServerTestUtils.GetRefreshForm(testScenario, create, refreshedAfterAppVisit.refresh_token);

            using var reuseResp = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(reuseForm));

            Assert.Equal(HttpStatusCode.BadRequest, reuseResp.StatusCode);
            string reuseJson = await reuseResp.Content.ReadAsStringAsync();
            var reuseErr = JsonSerializer.Deserialize<Dictionary<string, string>>(reuseJson);
            Assert.Equal("invalid_grant", reuseErr!["error"]);

            testScenario.SetLoginAttempt(2); // Set next login attempt to get new state/nonce and pkce values

            string authorizationRequestUrl2 = testScenario.GetAuthorizationRequestUrl();

            // This would be the URL Arbeidsflate redirects the user to. Now the user have a active Altinn Runtime cookie so the response will be a direct
            // redirect back to Arbeidsflate with code and state (no intermediate login at IdP).
            HttpResponseMessage authorizationRequestResponse2 = await client.GetAsync(authorizationRequestUrl2);

            string code2 = HttpUtility.ParseQueryString(authorizationRequestResponse2.Headers.Location!.Query)["code"]!;

            // Gets the new code from the callback response and redeems at /token endpoint
            Dictionary<string, string> tokenForm2 = OidcServerTestUtils.BuildTokenRequestForm(testScenario, code2);

            using var tokenResp2 = await client.PostAsync(
                "/authentication/api/v1/token",
                new FormUrlEncodedContent(tokenForm2));

            Assert.Equal(HttpStatusCode.OK, tokenResp2.StatusCode);
            var json2 = await tokenResp2.Content.ReadAsStringAsync();
            TokenResponseDto? tokenResult2 = JsonSerializer.Deserialize<TokenResponseDto>(json2);

            // Asserts on token response structure
            string sid2 = TokenAssertsHelper.AssertTokenResponse(tokenResult2, testScenario, _fakeTime.GetUtcNow());

            Assert.Equal(sid, sid2); // should be same session as before

            // ===== Phase 11: User is done for the day. Press Logout in Arbeidsflate. This should log the user out both in Altinn and Idporten. =====

            // Verify that session is active before logout
            OidcSession? beforeLoggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            OidcAssertHelper.AssertValidSession(beforeLoggedOutSession, testScenario, _fakeTime.GetUtcNow());
            Debug.Assert(beforeLoggedOutSession != null);

            using var logoutResp = await client.GetAsync(
                "/authentication/api/v1/openid/logout?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout?client_id=345345s&post_logout_redirect_uri=http%3a%2f%2flocalhost%2fauthentication%2fapi%2fv1%2flogout%2fhandleloggedout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sid, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);

            _downstreamLogoutClient.Verify(
            q => q.TryLogout(
            It.IsAny<OidcClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
        }

        private static string GetConfigPath()
        {
            string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            Debug.Assert(unitTestFolder != null, nameof(unitTestFolder) + " != null");
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private async Task<(string? UpstreamState, UpstreamLoginTransaction? CreatedUpstreamLogingTransaction)> AssertAutorizeRequestResult(OidcTestScenario testScenario, HttpResponseMessage authorizationRequestResponse, DateTimeOffset now)
        {
            OidcAssertHelper.AssertAuthorizeResponse(authorizationRequestResponse);
            string? upstreamState = HttpUtility.ParseQueryString(authorizationRequestResponse.Headers.Location!.Query)["state"];

            UpstreamLoginTransaction? createdUpstreamLogingTransaction = await OidcServerDatabaseUtil.GetUpstreamTransaction(upstreamState, DataSource);
            Assert.NotNull(createdUpstreamLogingTransaction);
            OidcAssertHelper.AssertUpstreamLoginTransaction(createdUpstreamLogingTransaction, testScenario, now);

            if (createdUpstreamLogingTransaction.RequestId != null)
            {
                // Asserting DB persistence after /authorize
                Debug.Assert(testScenario?.DownstreamClientId != null);
                LoginTransaction? loginTransaction = await OidcServerDatabaseUtil.GetDownstreamTransaction(testScenario.DownstreamClientId, testScenario.GetDownstreamState(), DataSource);
                OidcAssertHelper.AssertLoginTransaction(loginTransaction, testScenario, now);
            }

            return (upstreamState, createdUpstreamLogingTransaction);
        }

        private void ConfigureSblTokenMockByScenario(OidcTestScenario scenario)
        {
            string acrJoined = (scenario.Acr is { Count: > 0 }) ? string.Join(string.Empty, scenario.Acr) : string.Empty;
            string amrJoined = (scenario.Amr is { Count: > 0 }) ? string.Join(string.Empty, scenario.Amr) : string.Empty;

            UserAuthenticationModel userAuthenticationModel = new()
            {
                IsAuthenticated = true,
                AuthenticationLevel = AuthenticationHelper.GetAuthenticationLevelForIdPorten(acrJoined),
                AuthenticationMethod = AuthenticationHelper.GetAuthenticationMethod(amrJoined),
                PartyID = scenario.PartyId,
                UserID = scenario.UserId,
                Username = scenario.UserName
            };

            _cookieDecryptionService.Setup(s => s.DecryptTicket(It.IsAny<string>())).ReturnsAsync(userAuthenticationModel);
        }

        private void ConfigureMockProviderTokenResponse(OidcTestScenario testScenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            Guid upstreamSID = Guid.NewGuid();
            OidcCodeResponse oidcCodeResponse = IDProviderTestTokenUtil.GetIdPortenTokenResponse(
                testScenario.Ssn, 
                createdUpstreamLogingTransaction.Nonce, 
                upstreamSID.ToString(),
                testScenario.Acr.ToArray(), 
                testScenario.Amr?.ToArray(),
                createdUpstreamLogingTransaction.UpstreamClientId, 
                createdUpstreamLogingTransaction.Scopes,
                authTime);

            Mocks.OidcProviderAdvancedMock mock = Assert.IsType<Mocks.OidcProviderAdvancedMock>(
                Services.GetRequiredService<IOidcProvider>());
            var idpAuthCode = testScenario.GetUpstreamProviderCode(); // what we will pass on callback

            mock.SetupSuccess(
                authorizationCode: idpAuthCode,
                clientId: createdUpstreamLogingTransaction.UpstreamClientId,
                redirectUri: createdUpstreamLogingTransaction.UpstreamRedirectUri.ToString(),
                codeVerifier: createdUpstreamLogingTransaction.CodeVerifier,
                response: oidcCodeResponse);
        }

        private void ConfigureMockUidpProviderTokenResponseUidp(OidcTestScenario testScenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            OidcCodeResponse oidcCodeResponse = IDProviderTestTokenUtil.GetUidpTokenResponse(testScenario, createdUpstreamLogingTransaction, authTime);

            Mocks.OidcProviderAdvancedMock mock = Assert.IsType<Mocks.OidcProviderAdvancedMock>(
                Services.GetRequiredService<IOidcProvider>());
            var idpAuthCode = testScenario.GetUpstreamProviderCode(); // what we will pass on callback

            mock.SetupSuccess(
                authorizationCode: idpAuthCode,
                clientId: createdUpstreamLogingTransaction.UpstreamClientId,
                redirectUri: createdUpstreamLogingTransaction.UpstreamRedirectUri.ToString(),
                codeVerifier: createdUpstreamLogingTransaction.CodeVerifier,
                response: oidcCodeResponse);
        }

        private HttpClient CreateClientWithHeaders(Dictionary<string, string>? cookies = null)
        {
            var client = CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30000);

            // Headers used by the controller to capture IP/UA/correlation
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AltinnTestClient/1.0");
            client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42"); // Test IP

            if (cookies is not null && cookies.Count > 0)
            {
                // Build a single Cookie header: "name=value; name2=value2"
                var pairs = cookies.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}");
                var headerValue = string.Join("; ", pairs);
                client.DefaultRequestHeaders.Remove("Cookie"); // in case called twice
                client.DefaultRequestHeaders.Add("Cookie", headerValue);
            }

            return client;
        }

        private async Task ConfigureProfileMock(OidcTestScenario oidcTestScenario)
        {
            ProfileFileMock profileMock = new ProfileFileMock();
            if (!string.IsNullOrEmpty(oidcTestScenario.ExternalIdentity) && oidcTestScenario.UserId == null)
            {
                UserProfile? profile = null;
                _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(profile);

                Guid partyGuid = Guid.NewGuid();

                _userProfileService
                        .Setup(s => s.CreateUser(It.IsAny<UserProfile>()))
                        .ReturnsAsync((UserProfile input) => new UserProfile
                        {
                            // copy from the input
                            UserName = input.UserName,
                            ExternalIdentity = input.ExternalIdentity,
                            UserId = 123456,
                            PartyId = 123456,
                           
                            Party = new Party()
                            {
                                PartyUuid = partyGuid
                            },
                            UserUuid = partyGuid
                        });

            }
            else
            {
                UserProfile profile = await profileMock.GetUserProfile(new UserProfileLookup() { UserId = oidcTestScenario.UserId.Value });
                profile.ExternalIdentity = oidcTestScenario.ExternalIdentity;
                _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(profile);
            }
        }
    }
}
