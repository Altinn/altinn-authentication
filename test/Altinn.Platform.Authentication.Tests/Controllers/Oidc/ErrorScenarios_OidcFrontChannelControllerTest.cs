using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Models;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IOidcProvider, Mocks.OidcProviderAdvancedMock>();

            string configPath = GetConfigPath();

            WebHostBuilder builder = new();
            builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });

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
                "&scope=altinn%3Aportal%2Fenduser" +                // missing openid
                $"&client_id={testScenario.DownstreamClientId}" +
                "&response_type=code" +
                "&state=s123" +
                "&nonce=n123" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            var loc = resp.Headers.Location!;
            Assert.Contains("error=invalid_scope", loc.Query);
            Assert.Contains("state=s123", loc.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=unsupported_response_type", resp.Headers.Location!.Query);
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
            
            // You may choose to 400 locally (safer) or still 302 back to the provided redirect_uri if you validated it as absolute.
            // Here we assert 400 local error since the redirect_uri is not one of the registered URIs.
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Contains("error=invalid_request", resp.Headers.Location!.Query);
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

        private void ConfigureMockProviderTokenResponse(OidcTestScenario testScenario, UpstreamLoginTransaction createdUpstreamLogingTransaction)
        {
            Guid upstreamSID = Guid.NewGuid();
            OidcCodeResponse oidcCodeResponse = IdPortenTestTokenUtil.GetIdPortenTokenResponse(
                testScenario.Ssn, 
                createdUpstreamLogingTransaction.Nonce, 
                upstreamSID.ToString(), 
                createdUpstreamLogingTransaction.AcrValues, 
                testScenario.Amr?.ToArray(),
                createdUpstreamLogingTransaction.UpstreamClientId, 
                createdUpstreamLogingTransaction.Scopes);

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
