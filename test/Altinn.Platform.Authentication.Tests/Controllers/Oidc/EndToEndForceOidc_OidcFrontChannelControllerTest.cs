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
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
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

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        private readonly Mock<ISblCookieDecryptionService> _cookieDecryptionService = new();

        private static readonly FakeTimeProvider _fakeTime = new(
        DateTimeOffset.Parse("2025-03-01T08:00:00Z")); // any stable baseline for tests

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
            services.AddSingleton(_cookieDecryptionService.Object);

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
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.UpstreamProviderCode)}&state={Uri.EscapeDataString(upstreamState!)}";

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
            Assert.Equal(string.Join(' ', testScenario.Scopes), refreshed.scope);

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

            string refreshToken2 = await cookieRefreshResponse.Content.ReadAsStringAsync();
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
                "/authentication/api/v1/logout2?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
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
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.UpstreamProviderCode)}&state={Uri.EscapeDataString(upstreamState!)}";

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
                Assert.Equal(string.Join(' ', testScenario.Scopes), tokenResult.scope);
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
                "/authentication/api/v1/logout2?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
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
        /// 5) The user logs out from Arbeidsflate via <c>/authentication/api/v1/logout2</c>. The test verifies:
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
            Assert.StartsWith("https://login.idporten.no/authorize?response_type=code&client_id=345345s&redirect_uri=https%3a%2f%2fplatform.altinn.no%2fauthentication%2fapi", app2RedirectResponse.Headers.Location!.ToString());

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
            ConfigureMockProviderTokenResponse(testScenario, createdUpstreamLogingTransaction, _fakeTime.GetUtcNow());

            // === Phase 2: simulate provider redirecting back to Altinn with code + upstream state ===
            // Our proxy service (below) will fabricate a downstream code and redirect to the original app that was requested. 
            string callbackUrl = $"/authentication/api/v1/upstream/callback?code={Uri.EscapeDataString(testScenario.UpstreamProviderCode!)}&state={Uri.EscapeDataString(upstreamState!)}";

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
                "/authentication/api/v1/logout2?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("https://login.idporten.no/logout", logoutResp.Headers.Location!.ToString());

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);

            // Simulate that ID-provider call the front channel logout endpoint
            using var frontChannelLogoutResp = await client.GetAsync(
                $"/authentication/api/v1/upstream/frontchannel-logout?iss={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamIssuer)}&sid={HttpUtility.UrlEncode(beforeLoggedOutSession.UpstreamSessionSid!)}");

            string frontChannelContent = await frontChannelLogoutResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, frontChannelLogoutResp.StatusCode);
            Assert.Equal("OK", frontChannelContent);
        }

        private async Task<(string? UpstreamState, UpstreamLoginTransaction? CreatedUpstreamLogingTransaction)> AssertAutorizeRequestResult(OidcTestScenario testScenario, HttpResponseMessage authorizationRequestResponse, DateTimeOffset now)
        {
            OidcAssertHelper.AssertAuthorizeResponse(authorizationRequestResponse);
            string? upstreamState = HttpUtility.ParseQueryString(authorizationRequestResponse.Headers.Location!.Query)["state"];

            UpstreamLoginTransaction? createdUpstreamLogingTransaction = await OidcServerDatabaseUtil.GetUpstreamtransactrion(upstreamState, DataSource);
            Assert.NotNull(createdUpstreamLogingTransaction);
            OidcAssertHelper.AssertUpstreamLogingTransaction(createdUpstreamLogingTransaction, testScenario, now);

            if (createdUpstreamLogingTransaction.RequestId != null)
            {
                // Asserting DB persistence after /authorize
                Debug.Assert(testScenario?.DownstreamClientId != null);
                LoginTransaction? loginTransaction = await OidcServerDatabaseUtil.GetDownstreamTransaction(testScenario.DownstreamClientId, testScenario.GetDownstreamState(), DataSource);
                OidcAssertHelper.AssertLogingTransaction(loginTransaction, testScenario, now);
            }

            return (upstreamState, createdUpstreamLogingTransaction);
        }

        /// <summary>
        /// Test that verifies flow when it startes with Altinn 2 login
        /// </summary>
        /// <returns></returns>
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
                "/authentication/api/v1/logout2?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        /// <summary>
        /// Test that verifies flow when it startes with Altinn 2 login
        /// </summary>
        /// <returns></returns>
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
                "/authentication/api/v1/logout2?post_logout_redirect_uri=https%3A%2F%2Farbeidsflate.apps.localhost%2Floggetut&state=987654321");
            string content = await logoutResp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Found, logoutResp.StatusCode);
            Assert.StartsWith("http://localhost/ui/authentication/logout", logoutResp.Headers.Location!.ToString());
            OidcAssertHelper.AssertLogoutRedirect(logoutResp, testScenario);

            OidcSession? loggedOutSession = await OidcServerDatabaseUtil.GetOidcSessionAsync(sidFromCodeResponse, DataSource);
            Assert.Null(loggedOutSession);
        }

        private static string GetConfigPath()
        {
            string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            Debug.Assert(unitTestFolder != null, nameof(unitTestFolder) + " != null");
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private void ConfigureSblTokenMockByScenario(OidcTestScenario scenario)
        {
            UserAuthenticationModel userAuthenticationModel = new UserAuthenticationModel
            {
                IsAuthenticated = true,
                AuthenticationLevel = AuthenticationHelper.GetAuthenticationLevelForIdPorten(string.Join(string.Empty, scenario.Acr)),
                AuthenticationMethod = AuthenticationHelper.GetAuthenticationMethod(string.Join(string.Empty, scenario.Amr)),
                PartyID = scenario.PartyId,
                UserID = scenario.UserId,
                Username = scenario.UserName
            };

            _cookieDecryptionService.Setup(s => s.DecryptTicket(It.IsAny<string>())).ReturnsAsync(userAuthenticationModel);
        }

        private void ConfigureMockProviderTokenResponse(OidcTestScenario testScenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            Guid upstreamSID = Guid.NewGuid();
            OidcCodeResponse oidcCodeResponse = IdPortenTestTokenUtil.GetIdPortenTokenResponse(
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
            var idpAuthCode = testScenario.UpstreamProviderCode; // what we will pass on callback

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
    }
}
