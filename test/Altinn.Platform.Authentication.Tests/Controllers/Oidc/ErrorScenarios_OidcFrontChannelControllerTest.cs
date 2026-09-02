using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Models;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers.Oidc
{
    /// <summary>
    /// Front channel tests for <see cref="Authentication.Controllers.OidcFrontChannelController"/>.
    /// </summary>
    public class ErrorScenarios_OidcFrontChannelControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected IOidcServerClientRepository Repository => Services.GetRequiredService<IOidcServerClientRepository>();

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        private Mocks.OidcProviderAdvancedMock UpstreamProviderMock => (Mocks.OidcProviderAdvancedMock)Services.GetRequiredService<IOidcProvider>();

        private FakeTimeProvider _fakeTime = null!;

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            _fakeTime = new(DateTimeOffset.Parse("2025-03-01T08:00:00Z")); // any stable baseline for tests

            services.AddSingleton<IOidcProvider, Mocks.OidcProviderAdvancedMock>();
            services.AddSingleton<TimeProvider>(_fakeTime);

            string configPath = GetConfigPath();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");
            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
        }

        [Fact]
        public async Task Authorize_UnknownClient_Returns_LocalError400()
        {
            using var client = CreateClient();

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=does-not-exist" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode); // local error since client is unknown
        }

        [Fact]
        public async Task Authorize_MissingOpenIdScope_InvalidScope_ErrorRedirect()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=altinn%3Aportal%2Fenduser" + // missing openid
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_MissingCodeChallenge_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge_method=S256";  // challenge missing

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_WrongCodeChallengeMethod_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=plain"; // not allowed

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);        
        }

        [Fact]
        public async Task Authorize_PromptNoneWithLogin_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&prompt=none%20login" + // invalid combo
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_InvalidUiLocales_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&ui_locales=de%20fr" + // only nb/nn/en allowed
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_UnsupportedAcr_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&acr_values=foo-bar" + // not in allowed set
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_MissingNonce_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                
                // nonce missing
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_MissingState_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                
                // state missing
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_InvalidResponseType_UnsupportedResponseType()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=token" + // not supported
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_RedirectUri_NotRegistered_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var badRedirect = Uri.EscapeDataString("https://evil.example/steal");
            var url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={badRedirect}" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Authorize_MaxAge_Negative_InvalidRequest()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");

            // Insert matching client
            var create = NewClientCreate(testScenario);
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&max_age=-5" + // invalid
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        /// <summary>
        /// An upstream token exchange that fails (ID-porten refusing or failing to serve the
        /// code-to-token call) must come back to the client as an OIDC error redirect. It used to
        /// dereference the <c>null</c> response and escape as an unhandled 500.
        /// </summary>
        [Fact]
        public async Task UpstreamCallback_TokenExchangeFails_ErrorRedirectToClient()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");
            _ = await Repository.InsertClientAsync(NewClientCreate(testScenario));

            string upstreamState = await StartAuthorizeAndGetUpstreamState(client, testScenario);

            // The upstream refused us: the provider returns null, as it does for any 4xx/5xx/timeout.
            UpstreamProviderMock.SetupFailure(null, null, null, null);

            HttpResponseMessage callbackResp = await CallUpstreamCallback(client, testScenario, upstreamState);

            AssertTemporarilyUnavailableRedirect(callbackResp, testScenario);
        }

        /// <summary>
        /// Same requirement for the second half of the exchange: tokens that arrive but do not validate
        /// (signing-key rollover, issuer mismatch, expired) must not surface as an unhandled 500 either.
        /// </summary>
        [Fact]
        public async Task UpstreamCallback_UpstreamTokensDoNotValidate_ErrorRedirectToClient()
        {
            using var client = CreateClient();
            OidcTestScenario testScenario = OidcScenarioHelper.GetScenario("Arbeidsflate_HappyFlow");
            _ = await Repository.InsertClientAsync(NewClientCreate(testScenario));

            string upstreamState = await StartAuthorizeAndGetUpstreamState(client, testScenario);

            UpstreamProviderMock.SetupSuccess(
                null,
                null,
                null,
                null,
                new OidcCodeResponse { AccessToken = "not-a-jwt", IdToken = "not-a-jwt" });

            HttpResponseMessage callbackResp = await CallUpstreamCallback(client, testScenario, upstreamState);

            AssertTemporarilyUnavailableRedirect(callbackResp, testScenario);
        }

        private static void AssertTemporarilyUnavailableRedirect(HttpResponseMessage callbackResp, OidcTestScenario testScenario)
        {
            Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);

            Uri location = callbackResp.Headers.Location!; // asserted by the status code above
            Assert.StartsWith(testScenario.DownstreamClientCallbackUrl, location.ToString());

            NameValueCollection query = HttpUtility.ParseQueryString(location.Query);
            Assert.Equal("temporarily_unavailable", query["error"]);
            Assert.Equal(testScenario.GetDownstreamState(), query["state"]);
            Assert.Null(query["code"]);
        }

        private static async Task<HttpResponseMessage> CallUpstreamCallback(HttpClient client, OidcTestScenario testScenario, string upstreamState)
        {
            string callbackUrl =
                "/authentication/api/v1/upstream/callback" +
                $"?code={Uri.EscapeDataString(testScenario.GetUpstreamProviderCode())}" +
                $"&state={Uri.EscapeDataString(upstreamState)}";

            return await client.GetAsync(callbackUrl);
        }

        private static async Task<string> StartAuthorizeAndGetUpstreamState(HttpClient client, OidcTestScenario testScenario)
        {
            HttpResponseMessage authorizeResp = await client.GetAsync(testScenario.GetAuthorizationRequestUrl());
            Assert.Equal(HttpStatusCode.Redirect, authorizeResp.StatusCode);

            string? upstreamState = HttpUtility.ParseQueryString(authorizeResp.Headers.Location!.Query)["state"];
            Assert.False(string.IsNullOrEmpty(upstreamState));
            return upstreamState;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath)!; // assembly location always has a directory
            return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
        }

        private static OidcClientCreate NewClientCreate(OidcTestScenario testScenario) =>
            new()
            {
                ClientId = testScenario.DownstreamClientId!, // always set by OidcScenarioHelper.GetScenario
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
    }
}
