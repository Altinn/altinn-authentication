using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Altinn.Platform.Profile.Models;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Represents a collection of unit test with all integration tests of the <see cref="OpenIdController"/> class.
    /// </summary>
    public class LogoutControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private readonly WebApplicationFactory<LogoutController> _factory;
        private IConfiguration _configuration;
  
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<IOrganisationsService> _organisationsService = new();
        private readonly Mock<ISblCookieDecryptionService> _cookieDecryptionService = new();
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly Mock<TimeProvider> _timeProviderMock = new();
        private readonly Mock<IFeatureManager> _featureManager = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            string configPath = GetConfigPath();
            WebHostBuilder builder = new();

            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddJsonFile(configPath);
            });

            var configuration = new ConfigurationBuilder()
             .AddJsonFile(configPath)
             .AddInMemoryCollection(
               new Dictionary<string, string>
               {
                    { "GeneralSettings:EnableOidc", "false" },
                    { "GeneralSettings:ForceOidc", "false" },
                    { "GeneralSettings:DefaultOidcProvider", "altinn" },
                    { "FeatureManagement:EnableAuditLog", "false" }
               })
             .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton(_cookieDecryptionService.Object);
            services.AddSingleton(_userProfileService.Object);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
            services.AddSingleton(_featureManager.Object);
            services.AddSingleton(_eventQueue.Object);
            services.AddSingleton(_timeProviderMock.Object);
            services.AddSingleton(_organisationsService.Object);            
            _configuration = configuration;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="OpenIdControllerTests"/> class with the given WebApplicationFactory.
        /// </summary>
        /// <param name="factory">The WebApplicationFactory to use when creating a test server.</param>
        //public LogoutControllerTests(WebApplicationFactory<LogoutController> factory)
        //{
        //    _factory = factory;
        //    _userProfileService = new Mock<IUserProfileService>();
        //    _organisationsService = new Mock<IOrganisationsService>();
        //    _cookieDecryptionService = new Mock<ISblCookieDecryptionService>();
        //    SetupDateTimeMock();
        //}

        /// <summary>
        /// Validates that a user that is not authenticated is forward to SBL logout (not possible to identify any issorg)
        /// </summary>
        [Fact]
        public async Task Logout_TimedOut_RedirectToSBL()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("location", out values))
            {
                Assert.Equal("http://localhost/ui/authentication/logout", values.First());
            }
        }

        /// <summary>
        /// Validates that a user that is not authenticated is forward to SBL logout (not possible to identify any issorg)
        /// </summary>
        [Fact]
        public async Task Logout_LogedIn_RedirectToSBL()
        {
            string token = PrincipalUtil.GetToken(1337, null);

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("location", out values))
            {
                Assert.Equal("http://localhost/ui/authentication/logout", values.First());
            }
        }

        /// <summary>
        /// Validates that a user that is not authenticated is forward to SBL logout (not possible to identify any issorg)
        /// </summary>
        [Fact]
        public async Task Logout_LogedIn_RedirectToSBL_SelfIdentifiedUser()
        {
            string token = PrincipalUtil.GetSelfIdentifiedUserToken("siusertest", "12345", "2345678");

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("location", out values))
            {
                Assert.Equal("http://localhost/ui/authentication/logout", values.First());
            }
        }

        /// <summary>
        /// Validates that a user that is not authenticated is forward to SBL logout (not possible to identify any issorg)
        /// Event log : Audit log feature is turned on and the event is logged with expected claims
        /// </summary>
        [Fact]
        public async Task Logout_LogedIn_RedirectToSBL_ExternalAuthenticationMethod()
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim("originaliss", "uidp", ClaimValueTypes.String, issuer));
            claims.Add(new Claim("amr", AuthenticationMethod.BankID.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("acr", "Level4", ClaimValueTypes.String, issuer));

            string token = PrincipalUtil.GetToken(1337, claims);

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.BankID, SecurityLevel.VerySensitive, null, AuthenticationEventType.Logout, 1337);

            _featureManager
            .Setup(m => m.IsEnabledAsync("AuditLog"))
            .Returns(Task.FromResult(true));

            SetupDateTimeMock();
            
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("location", out values))
            {
                Assert.Equal("https://idporten.azurewebsites.net/api/v1/logout", values.First());
            }

            AssertionUtil.AssertAuthenticationEvent(_eventQueue, expectedAuthenticationEvent, Moq.Times.Once());
        }

        /// <summary>
        /// Validates that a user that is not authenticated is forward to SBL logout (not possible to identify any issorg)
        /// </summary>
        [Fact]
        public async Task Logout_LogedIn_RedirectToIss()
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim("originaliss", "uidp", ClaimValueTypes.String, issuer));

            string token = PrincipalUtil.GetToken(1337, claims);

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("location", out values))
            {
                Assert.Equal("https://idporten.azurewebsites.net/api/v1/logout", values.First());
            }

            if (response.Headers.TryGetValues("Set-Cookie", out values))
            {
                Assert.Equal(".ASPXAUTH=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.First());
                Assert.Equal("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.Last());
            }
        }

        /// <summary>
        /// Frontchannel logout
        /// Event log : Audit log feature is turned on and the event is logged
        /// </summary>
        [Fact]
        public async Task Logout_FrontChannelOK()
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim("originaliss", "uidp", ClaimValueTypes.String, issuer));

            string token = PrincipalUtil.GetToken(1337, claims);

            _eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.AltinnPIN, SecurityLevel.QuiteSensitive, null, AuthenticationEventType.Logout, 1337);

            _featureManager
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(true));

            SetupDateTimeMock();

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/frontchannel_logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            IEnumerable<string> values;

            if (response.Headers.TryGetValues("Set-Cookie", out values))
            {
                Assert.Equal(".ASPXAUTH=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.First());
                Assert.Equal("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.Last());
            }

            AssertionUtil.AssertAuthenticationEvent(_eventQueue, expectedAuthenticationEvent, Moq.Times.Once());
        }

        /// <summary>
        /// Frontchannel logout with event log feature turned off
        /// Event log : Audit log feature is turned off and the event is not logged
        /// </summary>
        [Fact]
        public async Task Logout_FrontChannelOK_Auditlog_off()
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim("originaliss", "uidp", ClaimValueTypes.String, issuer));

            string token = PrincipalUtil.GetToken(1337, claims);

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.AltinnPIN, SecurityLevel.QuiteSensitive, null, AuthenticationEventType.Logout, 1337);

            _featureManager
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(false));

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/frontchannel_logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            IEnumerable<string> values;

            if (response.Headers.TryGetValues("Set-Cookie", out values))
            {
                Assert.Equal(".ASPXAUTH=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.First());
                Assert.Equal("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.Last());
            }

            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Moq.Times.Never());
        }
       
        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private static AuthenticationEvent GetAuthenticationEvent(AuthenticationMethod authMethod, SecurityLevel authLevel, int? orgNumber, AuthenticationEventType authEventType, int? userId = null, bool isAuthenticated = true)
        {
            AuthenticationEvent authenticationEvent = new AuthenticationEvent();
            authenticationEvent.Created = new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero);
            authenticationEvent.AuthenticationMethod = authMethod;
            authenticationEvent.AuthenticationLevel = authLevel;
            authenticationEvent.OrgNumber = orgNumber;
            authenticationEvent.EventType = authEventType;            
            authenticationEvent.UserId = userId;
            authenticationEvent.IsAuthenticated = isAuthenticated;

            return authenticationEvent;
        }

        private void SetupDateTimeMock()
        {
            _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
        }
    }
}
