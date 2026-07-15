using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Altinn.Register.Contracts.V1;
using AltinnCore.Authentication.Constants;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using RegisterContracts = Altinn.Register.Contracts;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Represents a collection of unit test with all integration tests of the <see cref="AuthenticationController"/> class.
    /// </summary>
    public class AuthenticationControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private const string OrganisationIdentity = "OrganisationLogin";

        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<IGuidService> guidService = new();
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly Mock<IPartiesClient> _partiesClient = new();
        private IConfiguration _configuration = null!; // set in ConfigureServices

        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            builder.UseSetting("feature_management:feature_flags:0:id", "AuditLog");
            builder.UseSetting("feature_management:feature_flags:0:enabled", "true");
            base.ConfigureHost(builder);
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            string configPath = GetConfigPath();

            var configuration = new ConfigurationBuilder()
              .AddJsonFile(configPath)
              .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    { "GeneralSettings:DefaultOidcProvider", "altinn" }
                })
              .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));

            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton(_userProfileService);
            services.AddSingleton(_partiesClient.Object);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
            services.AddSingleton(_eventQueue.Object);            
            services.AddSingleton(guidService.Object);
            services.AddSingleton<IUserProfileService>(_userProfileService.Object);
            SetupGuidMock();
            _configuration = configuration;
        }

        protected override ValueTask InitializeAsync()
        {
            // Validation of tokens depend on current time. Remove this when https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/2572 has been resolved.
            TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
            return base.InitializeAsync();
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// Event log : Audit log feature is turned on and the event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateOrganisation_RequestTokenWithValidExternalToken_ReturnsNewToken()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";

            object iso6523Consumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{orgNr}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(iso6523Consumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:instances.write altinn:instances.read"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.MaskinPorten, SecurityLevel.Sensitive, 974760223, AuthenticationEventType.TokenExchange);

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);

            Assert.False(principal.HasClaim(c => c.Type == "urn:altinn:org"));
            AssertionUtil.AssertAuthenticationEvent(_eventQueue, expectedAuthenticationEvent, Times.Once());
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateOrganisation_RequestTokenWithInvalidIss_ReturnsNotAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";

            object iso6523Consumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{orgNr}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(iso6523Consumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:instances.write altinn:instances.read"));
            claims.Add(new Claim("iss", "https://ver3.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Enterprise-user (virksomhetsbruker) authentication was discontinued with the Altinn 2
        /// shutdown (#1979 / #2030): a Maskinporten exchange carrying the
        /// <c>X-Altinn-EnterpriseUser-Authentication</c> header now always returns 410 Gone.
        /// </summary>
        [Fact]
        public async Task EnterpriseUser_Header_ReturnsGone()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";

            object iso6523Consumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{orgNr}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(iso6523Consumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:instances.write altinn:instances.read"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VGVzdDpUZXN0ZXNlbg==");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateOrganisationWithSOScope_RequestTokenWithValidExternalToken_ReturnsNewToken()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";

            object iso6523Consumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{orgNr}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(iso6523Consumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:serviceowner/instances.read altinn:serviceowner/instances.write"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:org"));
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateOrganisationWithSoScope_RequestTestTokenWithValidExternalToken_ReturnsNewToken()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";
            string digdirOrgNo = "991825827";

            object digdirConsumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{digdirOrgNo}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(digdirConsumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:serviceowner/instances.read altinn:serviceowner/instances.write"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten?test=true");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:org"));

            Assert.Equal("ttd", principal.FindFirst(c => c.Type == "urn:altinn:org")!.Value); // non-null: HasClaim asserted above
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateExternalSystemToken_MissingBearerToken_NotAuthorized()
        {
            // Arrange
            HttpClient client = CreateClient();

            string url = "/authentication/api/v1/exchange/maskinporten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateExternalSystemToken_UnreadableBearerToken_NotAuthorized()
        {
            // Arrange
            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "ThisTokenShouldNotBeReadable");

            string url = "/authentication/api/v1/exchange/maskinporten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateExternalSystemToken_InvalidTokenProvider_NotAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>
            {
                new Claim("testClaim1", "testClaim1"),
                new Claim("testClaim2", "testClaim2")
            };

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);
            string token = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string tokenProvider = "google";
            string url = $"/authentication/api/v1/exchange/{tokenProvider}";

            string expectedMessage = $"Invalid token provider: {tokenProvider}. Trusted token providers are 'Maskinporten', 'Id-porten' and 'AltinnStudio'.";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);
            string message = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(expectedMessage, message);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// Event log : Audit log feature is turned on and the event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RequestTokenWithValidExternalToken_ReturnsNewToken()
        {
            // Arrange
            string expectedAuthLevel = "4";

            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "Level4";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("scope", "altinn:instances.read"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            RegisterContracts.Party? party = System.Text.Json.JsonSerializer.Deserialize<RegisterContracts.Party>(
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
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.NotNull(party);

            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(party);

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));

            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.MinIDPin, SecurityLevel.VerySensitive, null, AuthenticationEventType.TokenExchange, 20000);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());
            SecurityToken securityToken = JwtTokenMock.GetSecurityToken(token);
            SecurityToken securityTokenExternal = JwtTokenMock.GetSecurityToken(externalToken);

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:userid"));
            Assert.True(principal.HasClaim(c => c.Type == "pid"));
            Assert.Equal(expectedAuthLevel, principal.FindFirstValue("urn:altinn:authlevel"));
            Assert.Equal(securityTokenExternal.ValidTo, securityToken.ValidTo);
            AssertionUtil.AssertAuthenticationEvent(_eventQueue, expectedAuthenticationEvent, Times.Once());
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RequestTokenWithValidExternalTokenNewIdportenFormat_ReturnsNewToken()
        {
            // Arrange
            string expectedAuthLevel = "4";

            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("scope", "oidc altinn:instances.read"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            RegisterContracts.Party? party = System.Text.Json.JsonSerializer.Deserialize<RegisterContracts.Party>(
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
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.NotNull(party);

            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(party);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());
            SecurityToken securityToken = JwtTokenMock.GetSecurityToken(token);
            SecurityToken securityTokenExternal = JwtTokenMock.GetSecurityToken(externalToken);

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:userid"));
            Assert.True(principal.HasClaim(c => c.Type == "pid"));
            Assert.Equal(expectedAuthLevel, principal.FindFirstValue("urn:altinn:authlevel"));
            Assert.Equal(securityTokenExternal.ValidTo, securityToken.ValidTo);
        }

        /// <summary>
        /// Needs to use correct scope for payment and stats. Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RequestTokenUsingDigDirScope()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("scope", "oidc digdir:instances:altinn:rocks"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        /// <summary>
        /// Needs to use correct scope for payment and stats. Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RequestTokenUsingValidPartnerScope()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("scope", "oidc skatteetaten:mvameldingvalidering"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            RegisterContracts.Party? party = System.Text.Json.JsonSerializer.Deserialize<RegisterContracts.Party>(
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
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.NotNull(party);

            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(party);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_RequestTokenMissingClaim_ReturnsUnauthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "MinId-PIN";
            claims.Add(new Claim("scope", "oidc altinn:instances.read"));

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_ServiceThrowsException_ReturnsNewToken()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "MinId-PIN";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("scope", "oidc altinn:instances.read"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception());

            string url = "/authentication/api/v1/exchange/id-porten";

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of mock method>.
        /// </summary>
        [Fact]
        public void TokenMock_VerifyEncryptedAndSignedToken()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "MinId-PIN";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateEncryptedAndSignedToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            ClaimsPrincipal claimsPrincipal = JwtTokenMock.ValidateEncryptedAndSignedToken(externalToken);
            Assert.Equal(externalPrincipal.Identity!.Name, claimsPrincipal.Identity!.Name); // non-null: both principals are constructed with an identity
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateStudioToken_ValidToken_ReturnsNewToken()
        {
            // Arrange
            string accessToken = JwtTokenMock.GenerateAccessToken("studio", "studio.designer", TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());

            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/altinnstudio");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:app"));
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateStudioToken_InvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            HttpClient client = CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "234234234234asasassdbadtoken");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/altinnstudio");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.RefreshJwtCookie"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateAltinn3Token_ValidToken_ReturnsNewToken_Refresh()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";
            string sid = "BHqitIevJmeX_IrOzmS1XOvAQAWlrTK2OioLnx43Kqw";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("sid", sid));
            claims.Add(new Claim("scope", "oidc altinn:instances.read"));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            RegisterContracts.Party? party = System.Text.Json.JsonSerializer.Deserialize<RegisterContracts.Party>(
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
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.NotNull(party);

            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(party);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Get the altinn token
            string token = await response.Content.ReadAsStringAsync();
            ClaimsPrincipal altinnTokenPrincipal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());
            string? altinnSessionId = altinnTokenPrincipal.FindFirstValue("sid");

            url = "/authentication/api/v1/refresh";

            HttpClient refreshClient = CreateClient();
            refreshClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage refreshedTokenMessage = await refreshClient.GetAsync(url);
            string refreshedToken = await refreshedTokenMessage.Content.ReadAsStringAsync();
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(refreshedToken, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);
            Assert.NotEqual(sid, principal.FindFirstValue("sid"));
            Assert.Equal(altinnSessionId, principal.FindFirstValue("sid"));
            Assert.True(principal.HasClaim(c => c.Type == "amr"));
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.RefreshJwtCookie"/>.
        /// </summary>
        [Fact]
        public async Task RefreshJwtCookie_ValidToken_ReturnsNewTokenWithPid_Refresh()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";
            string sid = "BHqitIevJmeX_IrOzmS1XOvAQAWlrTK2OioLnx43Kqw";

            string uuid = Guid.NewGuid().ToString();

            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("sid", sid));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserId, "20000"));
            claims.Add(new Claim("scope", "oidc altinn:instances.read"));
            claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, uuid));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            _partiesClient.Setup(u => u.GetPartyByUuId(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Party { PartyId = 50001, SSN = "12345678901" });

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            string url = "/authentication/api/v1/refresh?enrichPid=true";

            HttpClient refreshClient = CreateClient();
            refreshClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            HttpResponseMessage refreshedTokenMessage = await refreshClient.GetAsync(url);
            string refreshedToken = await refreshedTokenMessage.Content.ReadAsStringAsync();
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(refreshedToken, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);
            Assert.Equal("12345678901", principal.FindFirstValue("pid"));
            Assert.True(principal.HasClaim(c => c.Type == "amr"));
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath)!; // assembly location always has a directory
            return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
        }

        private AuthenticationEvent GetAuthenticationEvent(AuthenticationMethod authMethod, SecurityLevel authLevel, int? orgNumber, AuthenticationEventType authEventType, int? userId = null, bool isAuthenticated = true, string? externalSessionId = null)
        {
            AuthenticationEvent authenticationEvent = new AuthenticationEvent();
            authenticationEvent.Created = TimeProvider.GetUtcNow();
            authenticationEvent.AuthenticationMethod = authMethod;
            authenticationEvent.AuthenticationLevel = authLevel;
            authenticationEvent.OrgNumber = orgNumber;
            authenticationEvent.EventType = authEventType;            
            authenticationEvent.UserId = userId;
            authenticationEvent.IsAuthenticated = isAuthenticated;
            authenticationEvent.SessionId = "eaec330c-1e2d-4acb-8975-5f3eba12b2fb";
            authenticationEvent.ExternalSessionId = externalSessionId;

            return authenticationEvent;
        }

        private void SetupGuidMock()
        {
            guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
        }
    }
}
