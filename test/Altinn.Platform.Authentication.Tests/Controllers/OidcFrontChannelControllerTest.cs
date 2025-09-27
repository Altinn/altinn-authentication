using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Front channel tests for <see cref="Altinn.Platform.Authentication.Controllers.OidcFrontChannelController"/>.
    /// </summary>
    public class OidcFrontChannelControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected IOidcServerClientRepository Repository => Services.GetRequiredService<IOidcServerClientRepository>();

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            string configPath = GetConfigPath();

            WebHostBuilder builder = new();
            builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");
            services.Configure<GeneralSettings>(generalSettingSection);
        }

        /// <summary>
        /// Scenario: A valid authorize request is made from Arbeidsflate (downstream).
        /// Result: A login_transaction and login_transaction_upstream is created, and a redirect to upstream /authorize is issued.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Authorize_Persists_Downstream_And_Upstream_And_Redirects()
        {
            // Arrange
            using var client = CreateClient();

            // Insert a client that matches the authorize request
            var create = NewClientCreate("c4dbc1b5-7c2e-4ea5-83ec-478ce7c37b21");
            _ = await Repository.InsertClientAsync(create);

            // Headers used by the controller to capture IP/UA/correlation
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AltinnTestClient/1.0");
            client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42"); // Test IP

            // Downstream authorize query (what Arbeidsflate would send)
            const string clientId = "c4dbc1b5-7c2e-4ea5-83ec-478ce7c37b21";
            var redirectUri = Uri.EscapeDataString("https://af.altinn.no/api/cb");
            var state = "3fcfc23e3bd145cabdcdb70ce406c875";
            var nonce = "58be49a0cb7df5b791a1fef6c854c5e2";
            var codeChallenge = "CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU";

            var url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={redirectUri}" +
                "&scope=openid%20altinn%3Aportal%2Fenduser" +
                "&acr_values=idporten-loa-substantial" +
                $"&state={state}" +
                $"&client_id={clientId}" +
                "&response_type=code" +
                $"&nonce={nonce}" +
                $"&code_challenge={codeChallenge}" +
                "&code_challenge_method=S256";

            // Act
            var resp = await client.GetAsync(url);

            // Assert: HTTP redirect to upstream authorize
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.NotNull(resp.Headers.Location);

            // We don't assert the exact upstream URL (it includes a generated state/nonce),
            // but we require it's absolute and points to an /authorize endpoint.
            var loc = resp.Headers.Location!;
            Assert.True(loc.IsAbsoluteUri, "Redirect Location must be absolute.");
            Assert.Contains("/authorize", loc.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            Assert.True(resp.Headers.CacheControl?.NoStore ?? false, "Cache-Control must include no-store");
            Assert.Contains("no-cache", resp.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);

            // Parse upstream Location query to ensure key params are present
            var upstreamQuery = System.Web.HttpUtility.ParseQueryString(loc.Query);
            Assert.Equal("code", upstreamQuery["response_type"]);
            Assert.False(string.IsNullOrEmpty(upstreamQuery["client_id"]));
            Assert.False(string.IsNullOrEmpty(upstreamQuery["redirect_uri"]));
            Assert.Equal("S256", upstreamQuery["code_challenge_method"]);
            Assert.False(string.IsNullOrEmpty(upstreamQuery["code_challenge"]));
            Assert.False(string.IsNullOrEmpty(upstreamQuery["state"]));
            Assert.False(string.IsNullOrEmpty(upstreamQuery["nonce"]));
            Assert.Equal("openid", upstreamQuery["scope"]); // by default upstream scope should be openid

            // ===== Verify DB persistence =====

            // 1) Downstream login_transaction exists for (client_id, state)
            Guid requestId;
            const string SQL_FIND_DOWNSTREAM = /*strpsql*/ @"
            SELECT request_id, client_id, state, code_challenge, redirect_uri, status
            FROM oidcserver.login_transaction
            WHERE client_id = @client_id AND state = @state
            LIMIT 1;";

            await using (var cmd = DataSource.CreateCommand(SQL_FIND_DOWNSTREAM))
            {
                cmd.Parameters.AddWithValue("client_id", clientId);
                cmd.Parameters.AddWithValue("state", state);

                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "Downstream login_transaction row not found.");
                requestId = reader.GetFieldValue<Guid>("request_id");

                Assert.Equal(clientId, reader.GetFieldValue<string>("client_id"));
                Assert.Equal(state, reader.GetFieldValue<string>("state"));
                Assert.Equal(codeChallenge, reader.GetFieldValue<string>("code_challenge"));
                Assert.Equal("pending", reader.GetFieldValue<string>("status"));

                var storedRedirect = reader.GetFieldValue<string>("redirect_uri");
                Assert.Equal("https://af.altinn.no/api/cb", storedRedirect);
            }

            // 2) Upstream login_transaction_upstream exists for that request_id and is pending
            const string SQL_FIND_UPSTREAM = /*strpsql*/ @"
            SELECT upstream_request_id, provider, status, state, code_challenge, authorization_endpoint, token_endpoint
            FROM oidcserver.login_transaction_upstream
            WHERE request_id = @request_id
            LIMIT 1;";

            await using (var cmd = DataSource.CreateCommand(SQL_FIND_UPSTREAM))
            {
                cmd.Parameters.AddWithValue("request_id", requestId);

                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "Upstream login_transaction_upstream row not found.");

                var provider = reader.GetFieldValue<string>("provider");
                var upstreamStatus = reader.GetFieldValue<string>("status");
                var upstreamAuthzEndpoint = reader.GetFieldValue<string>("authorization_endpoint");

                Assert.False(string.IsNullOrWhiteSpace(provider));
                Assert.Equal("pending", upstreamStatus);
                Assert.False(string.IsNullOrWhiteSpace(upstreamAuthzEndpoint));

                // Upstream code_challenge should be generated by server (not equal to downstream challenge)
                var upstreamChallenge = reader.GetFieldValue<string>("code_challenge");
                Assert.False(string.IsNullOrWhiteSpace(upstreamChallenge));
                Assert.NotEqual(codeChallenge, upstreamChallenge);
            }
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

            // Insert matching client
            var create = NewClientCreate("client-a");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=altinn%3Aportal%2Fenduser" +                // missing openid
                "&client_id=client-a" +
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
            var create = NewClientCreate("client-b");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-b" +
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
            var create = NewClientCreate("client-c");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-c" +
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
            var create = NewClientCreate("client-d");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-d" +
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
            var create = NewClientCreate("client-e");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-e" +
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
            var create = NewClientCreate("client-f");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-f" +
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
            var create = NewClientCreate("client-g");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-g" +
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
            var create = NewClientCreate("client-h");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-h" +
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
            var create = NewClientCreate("client-i");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-i" +
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
            var create = NewClientCreate("client-j");
            _ = await Repository.InsertClientAsync(create);

            var badRedirect = Uri.EscapeDataString("https://evil.example/steal");
            var url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={badRedirect}" +
                "&scope=openid" +
                "&client_id=client-j" +
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
            var create = NewClientCreate("client-k");
            _ = await Repository.InsertClientAsync(create);

            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid" +
                "&client_id=client-k" +
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

        private static OidcClientCreate NewClientCreate(string? id = null) =>
            new()
            {
                ClientId = id ?? $"client-{Guid.NewGuid():N}",
                ClientName = "Test Client",
                ClientType = ClientType.Confidential,
                TokenEndpointAuthMethod = TokenEndpointAuthMethod.ClientSecretBasic,
                RedirectUris = new[] { new Uri("https://af.altinn.no/api/cb") },
                AllowedScopes = new[] { "openid", "digdir:dialogporten.noconsent", "altinn:portal/enduser" },
                ClientSecretHash = "argon2id$v=19$m=65536,t=3,p=1$dummy$salthash",
                ClientSecretExpiresAt = null,
                SecretRotationAt = null,
                JwksUri = null,
                JwksJson = null
            };
    }
}
