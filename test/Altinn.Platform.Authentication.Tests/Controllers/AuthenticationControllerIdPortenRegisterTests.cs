using System;
using System.Collections.Concurrent;
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
using Altinn.Authorization.ModelUtils;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Moq;
using Xunit;
using RegisterContracts = Altinn.Register.Contracts;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Covers the ID-porten token exchange resolving the user fields
    /// (<see cref="IPartiesClient.GetPartyIdentifiersAndUsernameByPersonIdentifier"/>) from Register.
    /// </summary>
    public class AuthenticationControllerIdPortenRegisterTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<IGuidService> _guidService = new();
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly Mock<IPartiesClient> _partiesClient = new();
        private readonly Mock<IRegisterUserProvisioningClient> _registerUserProvisioningClient = new();
        private readonly Mock<IFeatureManager> _featureManager = new();
        private readonly CapturingLogger<AuthenticationController> _controllerLogger = new();

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

            IConfiguration configuration = new ConfigurationBuilder()
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
            services.AddSingleton(_registerUserProvisioningClient.Object);

            // The feature manager is mocked rather than configured, so a single test can decide
            // whether the email-user exchange is on: ConfigureHost/ConfigureServices run before the
            // test body, but the flag is read per request, so a Setup inside the test still applies.
            _featureManager.Setup(f => f.IsEnabledAsync(FeatureFlags.AuditLog)).ReturnsAsync(true);
            services.AddSingleton(_featureManager.Object);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
            services.AddSingleton(_eventQueue.Object);
            services.AddSingleton(_guidService.Object);
            services.AddSingleton<IUserProfileService>(_userProfileService.Object);
            services.AddSingleton<ILogger<AuthenticationController>>(_controllerLogger);
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
            RegisterContracts.Party? party = JsonSerializer.Deserialize<RegisterContracts.Party>(
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
                  "dateOfDeath": null,
                  "user": { "userId": 20000, "username": "steph", "userIds": [ 20000 ] }
                }
                """,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(party);

            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            Assert.Equal("5c0656db-cf51-43a9-bd68-d8a55e7b6f3b", principal.FindFirstValue("urn:altinn:party:uuid"));
            Assert.Equal("4", principal.FindFirstValue("urn:altinn:authlevel"));

            _partiesClient.Verify(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// When the email-user toggle is enabled and the ID-porten token carries an <c>email</c> claim
        /// instead of a <c>pid</c>, the exchange provisions/looks up the self-identified user through
        /// Register and issues a token carrying that user's identifiers plus the email-identifier claims.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_SelfRegisteredEmailUser_ReturnsTokenWithEmailUserClaims()
        {
            // Arrange
            _featureManager
                .Setup(f => f.IsEnabledAsync(FeatureFlags.SupportIDTokenExchangeForSelfRegisteredEmailUsers))
                .ReturnsAsync(true);

            Guid partyUuid = Guid.Parse("2f9d2f0d-6f3a-4e2f-9c38-1f1b0a5f9e11");
            SelfIdentifiedUserProvisioningRequest? capturedRequest = null;

            _registerUserProvisioningClient
                .Setup(c => c.GetOrCreateUser(It.IsAny<SelfIdentifiedUserProvisioningRequest>(), It.IsAny<CancellationToken>()))
                .Callback((SelfIdentifiedUserProvisioningRequest req, CancellationToken _) => capturedRequest = req)
                .ReturnsAsync((SelfIdentifiedUserProvisioningRequest req, CancellationToken _) =>
                    new RegisterContracts.SelfIdentifiedUser
                    {
                        Uuid = partyUuid,
                        VersionId = 1UL,
                        PartyId = 50002U,
                        DisplayName = req.UserName,
                        CreatedAt = DateTimeOffset.UtcNow,
                        ModifiedAt = DateTimeOffset.UtcNow,
                        IsDeleted = false,
                        DeletedAt = FieldValue.Null,
                        User = new RegisterContracts.PartyUser(
                            userId: 20001U,
                            username: req.UserName,
                            userIds: ImmutableValueArray.Create(20001U)),
                    });

            // Note the mixed casing: the address is expected to reach Register lower-cased, so that an
            // exchange and a browser login converge on the same self-identified user.
            List<Claim> claims = new()
            {
                new Claim("email", "Test.Person@Example.COM"),
                new Claim("amr", "Selfregistered-email"),
                new Claim("acr", "selfregistered-email"),
                new Claim("scope", "altinn:instances.read"),
            };

            ClaimsIdentity identity = new();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new(identity);

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
            Assert.Equal("20001", principal.FindFirstValue("urn:altinn:userid"));
            Assert.Equal("epost:test.person@example.com", principal.FindFirstValue("urn:altinn:username"));
            Assert.Equal("50002", principal.FindFirstValue("urn:altinn:partyid"));
            Assert.Equal(partyUuid.ToString(), principal.FindFirstValue("urn:altinn:party:uuid"));

            // "selfregistered-email" maps to SecurityLevel.SelfIdentifed.
            Assert.Equal("0", principal.FindFirstValue("urn:altinn:authlevel"));

            // The upstream email claim is replaced by the normalized address - exactly once, so the
            // mixed-case original is gone - matching the email claim a browser login issues for the same
            // user. No person identifier is leaked into the Altinn token.
            Assert.Single(principal.FindAll("email"));
            Assert.Equal("test.person@example.com", principal.FindFirstValue("email"));
            Assert.Null(principal.FindFirstValue("pid"));

            // The external identity is the urn the browser sign-in flow provisions under.
            Assert.NotNull(capturedRequest);
            Assert.Equal(RegisterContracts.SelfIdentifiedUserType.IdPortenEmail, capturedRequest.SelfIdentifiedUserType);
            Assert.Equal("test.person@example.com", capturedRequest.Email);
            Assert.Equal("epost:test.person@example.com", capturedRequest.UserName);
            Assert.StartsWith("urn:altinn:person:idporten-email:", capturedRequest.ExternalIdentity);
            Assert.Equal(capturedRequest.ExternalIdentity, principal.FindFirstValue("urn:altinn:party:external-identifier"));

            // An email user is never looked up by person identifier.
            _partiesClient.Verify(
                p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// A failing Register provisioning call fails the exchange rather than issuing a token with an
        /// incomplete identity. The error is logged without the email address or the email-derived
        /// external identity, so no personal data ends up in the logs.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_SelfRegisteredEmailUser_ProvisioningFails_ReturnsUnauthorized()
        {
            // Arrange
            _featureManager
                .Setup(f => f.IsEnabledAsync(FeatureFlags.SupportIDTokenExchangeForSelfRegisteredEmailUsers))
                .ReturnsAsync(true);

            _registerUserProvisioningClient
                .Setup(c => c.GetOrCreateUser(It.IsAny<SelfIdentifiedUserProvisioningRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RegisterContracts.SelfIdentifiedUser?)null);

            List<Claim> claims = new()
            {
                new Claim("email", "test.person@example.com"),
                new Claim("amr", "Selfregistered-email"),
                new Claim("acr", "selfregistered-email"),
                new Claim("scope", "altinn:instances.read"),
            };

            ClaimsIdentity identity = new();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new(identity);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            CapturingLogger<AuthenticationController>.Entry error = Assert.Single(_controllerLogger.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal("ID-porten exchange: Register provisioning failed.", error.Message);
            Assert.All(error.State, kv =>
            {
                string? value = kv.Value?.ToString();
                Assert.DoesNotContain("test.person", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("urn:altinn:person:idporten-email", value ?? string.Empty);
            });
        }

        /// <summary>
        /// With the toggle disabled, a token carrying only an <c>email</c> claim is rejected and no user
        /// is provisioned.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_SelfRegisteredEmailUser_ToggleDisabled_ReturnsUnauthorized()
        {
            // Arrange
            _featureManager
                .Setup(f => f.IsEnabledAsync(FeatureFlags.SupportIDTokenExchangeForSelfRegisteredEmailUsers))
                .ReturnsAsync(false);

            HttpClient client = CreateClientWithExternalToken(
                new Claim("email", "test.person@example.com"),
                new Claim("amr", "Selfregistered-email"),
                new Claim("acr", "selfregistered-email"),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            _registerUserProvisioningClient.Verify(
                c => c.GetOrCreateUser(It.IsAny<SelfIdentifiedUserProvisioningRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// With the toggle enabled, a token carrying an <c>email</c> claim but no <c>pid</c> is only exchanged
        /// when its acr is <c>selfregistered-email</c>; any other acr is rejected and no user is provisioned.
        /// </summary>
        [Theory]
        [InlineData("idporten-loa-low")]
        [InlineData("idporten-loa-substantial")]
        [InlineData("idporten-loa-high")]
        public async Task AuthenticateEndUser_EmailWithoutPid_AcrNotSelfRegisteredEmail_ReturnsUnauthorized(string acr)
        {
            // Arrange
            _featureManager
                .Setup(f => f.IsEnabledAsync(FeatureFlags.SupportIDTokenExchangeForSelfRegisteredEmailUsers))
                .ReturnsAsync(true);

            HttpClient client = CreateClientWithExternalToken(
                new Claim("email", "test.person@example.com"),
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", acr),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            _registerUserProvisioningClient.Verify(
                c => c.GetOrCreateUser(It.IsAny<SelfIdentifiedUserProvisioningRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _partiesClient.Verify(
                p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// A token carrying neither a <c>pid</c> nor an <c>email</c> claim identifies no one and is rejected,
        /// without reaching Register.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_NeitherPidNorEmail_ReturnsUnauthorized()
        {
            // Arrange
            _featureManager
                .Setup(f => f.IsEnabledAsync(FeatureFlags.SupportIDTokenExchangeForSelfRegisteredEmailUsers))
                .ReturnsAsync(true);

            HttpClient client = CreateClientWithExternalToken(
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", "idporten-loa-high"),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            _partiesClient.Verify(
                c => c.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _registerUserProvisioningClient.Verify(
                c => c.GetOrCreateUser(It.IsAny<SelfIdentifiedUserProvisioningRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// The person path's not-found case: Register knows no party for the <c>pid</c>, so no token is issued.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_PersonNotFoundInRegister_ReturnsUnauthorized()
        {
            // Arrange
            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RegisterContracts.Party?)null);

            HttpClient client = CreateClientWithExternalToken(
                new Claim("pid", "19108000239"),
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", "idporten-loa-high"),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Register may return a party that has no associated Altinn user. There is no user id to put in the
        /// token, so the exchange is rejected rather than issuing a token with a missing identity.
        /// </summary>
        [Fact]
        public async Task AuthenticateEndUser_PartyWithoutAltinnUser_ReturnsUnauthorized()
        {
            // Arrange
            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePersonParty(withAltinnUser: false));

            HttpClient client = CreateClientWithExternalToken(
                new Claim("pid", "19108000239"),
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", "idporten-loa-high"),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Pins the acr -> Altinn authentication level mapping, including the fallback for an acr value the
        /// catalog does not know: it resolves to level 0, not to the last character of the acr string.
        /// </summary>
        [Theory]
        [InlineData("idporten-loa-low", "0")]
        [InlineData("idporten-loa-substantial", "3")]
        [InlineData("idporten-loa-high", "4")]
        [InlineData("some-unmapped-acr-value", "0")]
        public async Task AuthenticateEndUser_ResolvesAuthenticationLevelFromAcr(string acr, string expectedAuthLevel)
        {
            // Arrange
            _partiesClient
                .Setup(p => p.GetPartyIdentifiersAndUsernameByPersonIdentifier(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePersonParty());

            HttpClient client = CreateClientWithExternalToken(
                new Claim("pid", "19108000239"),
                new Claim("amr", "Minid-PIN"),
                new Claim("acr", acr),
                new Claim("scope", "altinn:instances.read"));

            // Act
            HttpResponseMessage response = await client.GetAsync("/authentication/api/v1/exchange/id-porten");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string token = await response.Content.ReadAsStringAsync();
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token, TimeProvider.GetUtcNow());

            Assert.NotNull(principal);
            Assert.Equal(expectedAuthLevel, principal.FindFirstValue("urn:altinn:authlevel"));
        }

        /// <summary>
        /// Builds a client whose Authorization header carries an ID-porten token with the given claims.
        /// </summary>
        private HttpClient CreateClientWithExternalToken(params Claim[] claims)
        {
            ClaimsIdentity identity = new();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new(identity);

            HttpClient client = CreateClient();

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2), now: TimeProvider.GetUtcNow());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            return client;
        }

        /// <summary>
        /// Builds a Register person party by deserialization, so the real polymorphic contract is exercised.
        /// </summary>
        /// <param name="withAltinnUser">When <c>false</c>, the party carries no <c>user</c> object.</param>
        private static RegisterContracts.Party CreatePersonParty(bool withAltinnUser = true)
        {
            string user = withAltinnUser
                ? """, "user": { "userId": 20000, "username": "steph", "userIds": [ 20000 ] }"""
                : string.Empty;

            RegisterContracts.Party? party = JsonSerializer.Deserialize<RegisterContracts.Party>(
                $$"""
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
                  "dateOfDeath": null{{user}}
                }
                """,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(party);
            return party;
        }

        /// <summary>
        /// Records every log call so a test can assert on what was (and was not) logged.
        /// </summary>
        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public record Entry(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State);

            public ConcurrentQueue<Entry> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                IReadOnlyList<KeyValuePair<string, object?>> values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
                Entries.Enqueue(new Entry(logLevel, formatter(state, exception), values));
            }
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerIdPortenRegisterTests).Assembly.Location).LocalPath)!; // assembly location always has a directory
            return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
        }
    }
}
