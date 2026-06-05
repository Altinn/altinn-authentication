using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using RegisterContracts = Altinn.Register.Contracts;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Covers the Register branch of the ID-porten token exchange, i.e. when the
    /// <see cref="FeatureFlags.IdPortenUserLookupFromRegister"/> flag is enabled and the user fields
    /// are resolved from Register (<see cref="IPartiesClient.GetPartyByPersonId"/>) instead of the
    /// platform Profile API. The flag-off (Profile) branch is covered by
    /// <see cref="AuthenticationControllerTests"/>.
    /// </summary>
    public class AuthenticationControllerIdPortenRegisterTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<ISblCookieDecryptionService> _cookieDecryptionService = new();
        private readonly Mock<IGuidService> _guidService = new();
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly Mock<IPartiesClient> _partiesClient = new();

        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            builder.UseSetting("feature_management:feature_flags:0:id", "AuditLog");
            builder.UseSetting("feature_management:feature_flags:0:enabled", "true");

            // Enable the Register-based ID-porten user lookup for this test class.
            builder.UseSetting("feature_management:feature_flags:1:id", FeatureFlags.IdPortenUserLookupFromRegister);
            builder.UseSetting("feature_management:feature_flags:1:enabled", "true");
            base.ConfigureHost(builder);
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            string configPath = GetConfigPath();

            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        { "GeneralSettings:EnableOidc", "false" },
                        { "GeneralSettings:ForceOidc", "false" },
                        { "GeneralSettings:DefaultOidcProvider", "altinn" }
                    })
                .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));

            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton(_cookieDecryptionService);
            services.AddSingleton(_userProfileService);
            services.AddSingleton(_partiesClient.Object);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
            services.AddSingleton(_eventQueue.Object);
            services.AddSingleton(_guidService.Object);
            services.AddSingleton<IUserProfileService>(_userProfileService.Object);
            services.AddSingleton<ISblCookieDecryptionService>(_cookieDecryptionService.Object);
            _guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
        }

        protected override ValueTask InitializeAsync()
        {
            // Token validation depends on current time.
            TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
            return base.InitializeAsync();
        }

        /// <summary>
        /// When the Register lookup flag is enabled, the ID-porten exchange resolves the user fields from
        /// Register and issues a token carrying UserId/UserName/PartyId from the returned party.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RegisterLookupEnabled_ReturnsTokenWithRegisterUserFields()
        {
            // Arrange
            List<Claim> claims = new()
            {
                new Claim("pid", "19108000239"),
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", "idporten-loa-high"),
                new Claim("scope", "altinn:instances.read"),
            };

            ClaimsIdentity identity = new();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new(identity);

            // The mock returns a Register party (deserialized to exercise the real polymorphic contract)
            // whose User object carries the Altinn user id and username.
            RegisterContracts.Party party = JsonSerializer.Deserialize<RegisterContracts.Party>(
                """
                {
                  "partyType": "person",
                  "partyUuid": "5c0656db-cf51-43a9-bd68-d8a55e7b6f3b",
                  "versionId": 1,
                  "partyId": 50001,
                  "personIdentifier": "19108000239",
                  "displayName": "Test Testesen",
                  "createdAt": "2020-01-01T00:00:00Z",
                  "modifiedAt": "2020-01-01T00:00:00Z",
                  "isDeleted": false,
                  "user": { "userId": 20000, "username": "steph", "userIds": [ 20000 ] }
                }
                """,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            _partiesClient
                .Setup(p => p.GetPartyByPersonId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(party);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string token = await response.Content.ReadAsStringAsync();
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);
            Assert.Equal("20000", principal.FindFirstValue("urn:altinn:userid"));
            Assert.Equal("steph", principal.FindFirstValue("urn:altinn:username"));
            Assert.Equal("50001", principal.FindFirstValue("urn:altinn:partyid"));
            Assert.Equal("4", principal.FindFirstValue("urn:altinn:authlevel"));

            _partiesClient.Verify(p => p.GetPartyByPersonId(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerIdPortenRegisterTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
        }
    }
}
