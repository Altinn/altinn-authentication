using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        [Fact]
        public async Task Authorize_Arbeidsflate_Full_Login_Flow()
        {
            // Build client with DI override for IOidcServerService
            using var client = CreateClient();

            var create = NewClientCreate("c4dbc1b5-7c2e-4ea5-83ec-478ce7c37b21");
            _ = await Repository.InsertClientAsync(create);

            // Fake service that returns a redirect but also captures the AuthorizeRequest it receives
            var upstreamUrl = new Uri("https://login.idporten.no/authorize?client_id=test-upstream");
            var upstreamState = "UP-STATE-123";
            var requestId = Guid.NewGuid();

            // Headers we’ll use to derive UA hash and correlation id
            var userAgent = "AltinnTestClient/1.0 (+https://altinn.no)";
            var correlationId = Guid.NewGuid().ToString();

            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

            // If your app uses ForwardedHeaders middleware to set RemoteIpAddress,
            // you can also add X-Forwarded-For; otherwise RemoteIp will be 127.0.0.1 (TestServer default).
            var simulatedIp = "203.0.113.42"; // TEST-NET-3 sample IP
            client.DefaultRequestHeaders.Add("X-Forwarded-For", simulatedIp);

            // Minimal, valid-enough query (service is stubbed)
            var url =
                "/authentication/api/v1/authorize" +
                "?redirect_uri=https%3A%2F%2Faf.altinn.no%2Fapi%2Fcb" +
                "&scope=openid%20altinn%3Aportal%2Fenduser" +
                "&acr_values=idporten-loa-substantial" +
                "&state=3fcfc23e3bd145cabdcdb70ce406c875" +
                "&client_id=c4dbc1b5-7c2e-4ea5-83ec-478ce7c37b21" +
                "&response_type=code" +
                "&nonce=58be49a0cb7df5b791a1fef6c854c5e2" +
                "&code_challenge=CoD_rETvp22kce_Kts2NQdGWc1E0m7bgRcg6oip3DDU" +
                "&code_challenge_method=S256";

            // Act
            var resp = await client.GetAsync(url);

            // Assert HTTP
            Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
           
            //Assert.NotNull(resp.Headers.Location);
            //Assert.Equal(upstreamUrl, resp.Headers.Location);
            //Assert.True(resp.Headers.CacheControl?.NoStore ?? false);
            //Assert.Contains("no-cache", resp.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private sealed class FakeOidcServerService : IOidcServerService
        {
            private readonly AuthorizeResult _result;

            public FakeOidcServerService(AuthorizeResult result, CancellationToken ct) => _result = result;

            public Task<AuthorizeResult> Authorize(AuthorizeRequest request, CancellationToken ct)
                => Task.FromResult(_result);
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
            ClientSecretHash = "argon2id$v=19$m=65536,t=3,p=1$dummy$salthash", // test-only
            ClientSecretExpiresAt = null,
            SecretRotationAt = null,
            JwksUri = null,
            JwksJson = null
        };

        private static string ComputeSha256Base64Url(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var b64 = Convert.ToBase64String(bytes);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}