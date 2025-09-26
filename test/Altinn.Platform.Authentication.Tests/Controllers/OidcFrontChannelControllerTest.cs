using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
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
        public async Task Authorize_WhenServiceReturnsRedirectUpstream_Responds302_WithExpectedLocation_AndNoStore()
        {
            // Arrange: fake service that forces a RedirectUpstream outcome
            var upstreamUrl = new Uri("https://login.idporten.no/authorize?client_id=test-upstream");
            var upstreamState = "UP-STATE-123";
            var requestId = Guid.NewGuid();

            var fakeService = new FakeOidcServerService(
                AuthorizeResult.RedirectUpstream(upstreamUrl, upstreamState, requestId));

            using var client = CreateClient();

            // Minimal, valid-enough query for the controller route (values won’t matter since we stub the service)
            var url =
                "/authentication/api/v1/authorize" +
                "?response_type=code" +
                "&client_id=arbeidsflate" +
                "&redirect_uri=https%3A%2F%2Farbeidsflate.example.no%2Fauth%2Fcallback" +
                "&scope=openid" +
                "&state=abc" +
                "&nonce=n" +
                "&code_challenge=xyz" +
                "&code_challenge_method=S256";

            // Act
            var resp = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.NotNull(resp.Headers.Location);
            Assert.Equal(upstreamUrl, resp.Headers.Location);

            // Cache headers for front-channel flows
            Assert.True(resp.Headers.CacheControl?.NoStore ?? false, "Cache-Control must include no-store");
            Assert.True(
                resp.Headers.Pragma.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase),
                "Pragma should include no-cache");
        }

        private sealed class FakeOidcServerService : IOidcServerService
        {
            private readonly AuthorizeResult _result;

            public FakeOidcServerService(AuthorizeResult result) => _result = result;

            public Task<AuthorizeResult> Authorize(AuthorizeRequest request)
                => Task.FromResult(_result);
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }
    }
}