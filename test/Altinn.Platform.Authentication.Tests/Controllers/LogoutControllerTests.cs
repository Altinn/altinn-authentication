using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly FakeTimeProvider _timeProviderMock = new();
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
                    { "GeneralSettings:DefaultOidcProvider", "altinn" },
                    { "FeatureManagement:EnableAuditLog", "false" }
               })
             .Build();

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton(_userProfileService.Object);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
            services.AddSingleton<IRequestSystemUser, RequestSystemUserServiceMock>();
            services.AddSingleton<IChangeRequestSystemUser, ChangeRequestSystemUserServiceMock>();
            services.AddSingleton(_featureManager.Object);
            services.AddSingleton(_eventQueue.Object);
            services.AddSingleton((TimeProvider)_timeProviderMock);
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
        //    SetupDateTimeMock();
        //}

        /// <summary>
        /// Validates that an unauthenticated user is redirected to BaseUrl (no OIDC provider can be resolved).
        /// </summary>
        [Fact]
        public async Task Logout_TimedOut_RedirectToBaseUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            Assert.True(response.Headers.TryGetValues("location", out IEnumerable<string>? values));
            Assert.Equal("http://localhost/", values!.First());
        }

        /// <summary>
        /// Validates that a logged-in user is redirected to BaseUrl when no OIDC provider can be resolved from the token.
        /// </summary>
        [Fact]
        public async Task Logout_LoggedIn_RedirectToBaseUrl()
        {
            string token = PrincipalUtil.GetToken(1337, null);

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            Assert.True(response.Headers.TryGetValues("location", out IEnumerable<string>? values));
            Assert.Equal("http://localhost/", values!.First());
        }

        /// <summary>
        /// Validates that a logged-in self-identified user is redirected to BaseUrl when no OIDC provider can be resolved.
        /// </summary>
        [Fact]
        public async Task Logout_LoggedIn_RedirectToBaseUrl_SelfIdentifiedUser()
        {
            string token = PrincipalUtil.GetSelfIdentifiedUserToken("siusertest", "12345", "2345678");

            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout");
            SetupUtil.AddAuthCookie(requestMessage, token);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            Assert.True(response.Headers.TryGetValues("location", out IEnumerable<string>? values));
            Assert.Equal("http://localhost/", values!.First());
        }

        /// <summary>
        /// Validates that a logged-in external-auth user with no active authorization-server session
        /// logs out to BaseUrl, and that the logout audit event is recorded when AuditLog is enabled.
        /// </summary>
        [Fact]
        public async Task Logout_LoggedIn_NoActiveSession_RedirectToBaseUrl_ExternalAuthenticationMethod()
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
                Assert.Equal("http://localhost/", values.First());
            }

            AssertionUtil.AssertAuthenticationEvent(_eventQueue, expectedAuthenticationEvent, Moq.Times.Once());
        }

        /// <summary>
        /// Validates that a logged-in user with no active authorization-server session logs out to
        /// BaseUrl, clearing the runtime auth cookie.
        /// </summary>
        [Fact]
        public async Task Logout_LoggedIn_NoActiveSession_RedirectToBaseUrl_ClearsCookie()
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
                Assert.Equal("http://localhost/", values.First());
            }

            if (response.Headers.TryGetValues("Set-Cookie", out values))
            {
                Assert.Contains(values, v => v.StartsWith("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT"));
            }
        }

        [Fact]
        public async Task Logout_HandleLoggedOut_RedirectToRequestRedirectUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout");
            requestMessage.Headers.Add("Cookie", "AltinnLogoutInfo=SystemuserRequestId=c0970300-005c-4784-aea6-5e7bac61b9b1");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> cookieValues);
            Assert.Equal("AltinnLogoutInfo=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", cookieValues?.First());

            response.Headers.TryGetValues("location", out IEnumerable<string> locationValues);
            Assert.Equal("https://smartcloudaltinn.azurewebsites.net/request", locationValues?.First());
        }

        [Fact]
        public async Task Logout_HandleLoggedOut_RedirectToChangeRequestRedirectUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout");
            requestMessage.Headers.Add("Cookie", "AltinnLogoutInfo=SystemuserChangeRequestId=c0970300-005c-4784-aea6-5e7bac61b9b1");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> cookieValues);
            Assert.Equal("AltinnLogoutInfo=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", cookieValues?.First());

            response.Headers.TryGetValues("location", out IEnumerable<string> locationValues);
            Assert.Equal("https://smartcloudaltinn.azurewebsites.net/changerequest", locationValues?.First());
        }

        [Fact]
        public async Task Logout_HandleLoggedOut_RedirectToAgentRequestRedirectUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout");
            requestMessage.Headers.Add("Cookie", "AltinnLogoutInfo=SystemuserAgentRequestId=c0970300-005c-4784-aea6-5e7bac61b9b1");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> cookieValues);
            Assert.Equal("AltinnLogoutInfo=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", cookieValues?.First());

            response.Headers.TryGetValues("location", out IEnumerable<string> locationValues);
            Assert.Equal("https://smartcloudaltinn.azurewebsites.net/agentrequest", locationValues?.First());
        }

        [Fact]
        public async Task Logout_HandleLoggedOut_RedirectToConsentRedirectUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout");
            requestMessage.Headers.Add("Cookie", "AltinnLogoutInfo=amSafeRedirectUrl=aHR0cHM6Ly9sb2NhbGhvc3Q=");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            response.Headers.TryGetValues("location", out IEnumerable<string> locationValues);
            Assert.Equal("https://am.ui.localhost/accessmanagement/api/v1/logoutredirect", locationValues?.First());
        }

        [Fact]
        public async Task Logout_HandleLoggedOut_RedirectToBaseUrl()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/logout/handleloggedout");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);

            response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> cookieValues);
            Assert.Equal("AltinnLogoutInfo=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", cookieValues?.First());

            response.Headers.TryGetValues("location", out IEnumerable<string> locationValues);
            Assert.Equal("http://localhost/", locationValues?.First());
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
                Assert.Equal("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.Single());
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
                Assert.Equal("AltinnStudioRuntime=; expires=Thu, 01 Jan 1970 00:00:00 GMT; domain=localhost; path=/; secure; httponly", values.Single());
            }

            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Moq.Times.Never());
        }
       
        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
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
            _timeProviderMock.SetUtcNow(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
        }
    }
}
