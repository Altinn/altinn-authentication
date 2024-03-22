using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.Utils;
using Altinn.Platform.Profile.Models;
using AltinnCore.Authentication.Constants;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

using Moq;
using Newtonsoft.Json;

using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Represents a collection of unit test with all integration tests of the <see cref="AuthenticationController"/> class.
    /// </summary>
    public class AuthenticationControllerTests : IClassFixture<WebApplicationFactory<AuthenticationController>>
    {
        private const string OrganisationIdentity = "OrganisationLogin";

        private readonly WebApplicationFactory<AuthenticationController> _factory;
        private readonly Mock<IUserProfileService> _userProfileService;
        private readonly Mock<ISblCookieDecryptionService> _cookieDecryptionService;
        private readonly Mock<TimeProvider> timeProviderMock = new Mock<TimeProvider>();
        private readonly Mock<IGuidService> guidService = new Mock<IGuidService>();

        /// <summary>
        /// Initialises a new instance of the <see cref="AuthenticationControllerTests"/> class with the given WebApplicationFactory.
        /// </summary>
        /// <param name="factory">The WebApplicationFactory to use when creating a test server.</param>
        public AuthenticationControllerTests(WebApplicationFactory<AuthenticationController> factory)
        {
            _factory = factory;
            _userProfileService = new Mock<IUserProfileService>();
            _cookieDecryptionService = new Mock<ISblCookieDecryptionService>();
            SetupDateTimeMock();
            SetupGuidMock();
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.MaskinPorten, SecurityLevel.Sensitive, 974760223, AuthenticationEventType.TokenExchange);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);

            Assert.NotNull(principal);

            Assert.False(principal.HasClaim(c => c.Type == "urn:altinn:org"));
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEnterpriseUser_RequestToken_ReturnsTooManyRequests()
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VGVzdDpUZXN0ZXNlbg==");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEnterpriseUser_RequestTokenWithInvalidBase64_ReturnsBadRequest()
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "InvalidBase64");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateEnterpriseUser_RequestTokenWithInvalidPassword_ReturnsUnauthorized()
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VGVzdDpXcm9uZ1Bhc3N3b3Jk");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// Event log : Audit log feature is turned on and the event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateEnterpriseUser_RequestToken_ReturnsOK()
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
            claims.Add(new Claim("jti", "fe155387-c5f2-42e9-943a-811789db663a"));

            ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.VirksomhetsBruker, SecurityLevel.Sensitive, 974760223, AuthenticationEventType.TokenExchange, 1234, true, "fe155387-c5f2-42e9-943a-811789db663a");

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// Event log : Audit log feature is turned on and the event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateEnterpriseUser_RequestToken_PasswordContainsColon_ReturnsOK()
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            Mock<IGuidService> guidService = new Mock<IGuidService>();
            guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");

            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.VirksomhetsBruker, SecurityLevel.Sensitive, 974760223, AuthenticationEventType.TokenExchange, 1234);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyMjpWYWxpZDpQYXNzd29yZA==");

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");           

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);

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

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/maskinporten?test=true");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:org"));

            Assert.Equal("ttd", principal.FindFirst(c => c.Type == "urn:altinn:org").Value);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.AuthenticateUser"/>.
        /// Event log : Audit log feature is turned on and the event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateUser_RequestTokenWithValidAltinnCookie_ReturnsNewToken()
        {
            // Arrange
            UserAuthenticationModel userAuthenticationModel = new UserAuthenticationModel
            {
                IsAuthenticated = true,
                AuthenticationLevel = SecurityLevel.QuiteSensitive,
                AuthenticationMethod = AuthenticationMethod.AltinnPIN,
                PartyID = 23,
                UserID = 434,
                Username = "bob"
            };

            _cookieDecryptionService.Setup(s => s.DecryptTicket(It.IsAny<string>())).ReturnsAsync(userAuthenticationModel);

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.AltinnPIN, SecurityLevel.QuiteSensitive, null, AuthenticationEventType.Authenticate, 434, true);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object);

            string url = "/authentication/api/v1/authentication?goto=http%3A%2F%2Flocalhost";
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add("Cookie", ".ASPXAUTH=asdasdasd");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);

            string token = null;
            string sameSite = null;
            bool httpOnly = false;
            bool sessionCookie = true;

            response.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> cookies);
            foreach (string cookie in cookies)
            {
                string[] cookieParts = cookie.Split("; ");
                foreach (string cookiePart in cookieParts)
                {
                    string[] cookieKeyValue = cookiePart.Split('=');

                    switch (cookieKeyValue[0])
                    {
                        case "AltinnStudioRuntime":
                            token = cookieKeyValue[1];
                            break;
                        case "httponly":
                            httpOnly = true;
                            break;
                        case "expires":
                            // Cookies WITHOUT 'expires' are session cookies. They are gone when the browser is closed.
                            sessionCookie = false;
                            break;
                        case "samesite":
                            sameSite = cookieKeyValue[1];
                            break;
                    }
                }
            }

            Assert.NotNull(token);
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);
            Assert.NotNull(principal);

            Assert.NotNull(sameSite);
            Assert.Equal("lax", sameSite);

            Assert.True(httpOnly);
            Assert.True(sessionCookie);
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.AuthenticateUser"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateUser_NoTokenPortalParametersIncluded_RedirectsToSBL()
        {
            // Arrange         
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            string url = "/authentication/api/v1/authentication?goto=http%3A%2F%2Flocalhost&DontChooseReportee=true";
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string expectedLocation = "http://localhost/ui/authentication?goTo=http%3a%2f%2flocalhost%2fauthentication%2fapi%2fv1%2fauthentication%3fgoto%3dhttp%3a%2f%2flocalhost%26DontChooseReportee%3dtrue";
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(expectedLocation, response.Headers.Location.ToString());
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component with no ISS given
        /// 3. OIDC is enabled and the default authentication mechanismen
        /// Expections: Authentication components redirects to default OIDC provider with all neded params
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDC_NoTokenPortalParametersIncludedOIDCDefaultEnabled_RedirectsToOIDCProvider()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Act 2
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(requestMessage);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idprovider.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "2314534634r2", out string stateParam, out string nonceParam, out string redirectUriParam);
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component with no ISS given
        /// 3. OIDC is enabled and the default authentication mechanismen
        /// Expections: Authentication components redirects to default OIDC provider with all neded params
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDC_NoTokenPortalParametersIncludedOIDCDefaultEnabled_RedirectsToOIDCProvider2()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true, "idporten");
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Act 2
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(requestMessage);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idporten.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "345345s", out string stateParam, out string nonceParam, out string redirectUriParam);
        }

        /// <summary>
        /// This test veryf the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component
        /// 3. OIDC is enabled and is the default authentication component
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        ///  In real life the ODIC provider will then authenticate user and redirect user back to authentication component
        ///  In this test the authorization code is created in test
        ///  5. User is redirectet back to authentication component with state and XSRF cookie
        ///  6. Authenticaiton component verifies XSRF header and cookie
        ///  7. Autentication component calls OIDC provider with code to exchange it to a token
        ///  8. Authentication compoment verifies token and create authentication info
        ///  9. Authentication component creates altinn 3 token and puts it in to a cookie
        ///  10. Redirects back to original app
        ///  11. Verify the authentication event is logged with expected claims and event type
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDC_FullProcess_RedirectsToOIDCAndBackWithValidToken()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            
            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));
            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.BankIDMobil, SecurityLevel.VerySensitive, null, AuthenticationEventType.Authenticate, 1337, true);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Call Authentication component equalt to browsr beeing redirect from Altinn app
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idprovider.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify GoToCookie
            ValidateGoToCookie(redirectToOidcProviderResponse, HttpUtility.UrlEncode(gotoUrl));

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "2314534634r2", out string stateParam, out string nonceParam, out string redirectUriParam);

            // This part is the where we prepare the response as a OIDC Provider would do.
            // When returned from the OIDC Provider user will have an Authorization code. This code can have
            // different formats and this component does not need to understand it. It just exchanged code with
            // OIDC Provider to get a JWT ID token in response. In this test we create a JWTToken as code to make
            // the exchange in OidcProviderMock simple
            string authorizationCode = CreateOidcCode("1337", "1337", nonceParam);
            string redirectFromOidcProviderUri = GetAuthenticationUrlWithToken(redirectUriParam, stateParam, authorizationCode, "https://idprovider.azurewebsites.net/");
            HttpRequestMessage redirectFromOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, redirectFromOidcProviderUri);

            // Act 2. This simulates the request the browser will do when user is authenticated at OIDC provider and returns to Altinn authentication.
            HttpResponseMessage redirectFromOidcProviderResponse = await client.SendAsync(redirectFromOidcProviderRequest);

            // Assert: Now the user should be redirected back to original requested app.
            Assert.Equal(HttpStatusCode.Redirect, redirectFromOidcProviderResponse.StatusCode);
            Assert.StartsWith(gotoUrl, redirectFromOidcProviderResponse.Headers.Location.ToString());

            // Check to see if platform cookie is set with token and verify token and claims
            redirectFromOidcProviderResponse.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> cookieHeaders);
            Assert.NotEmpty(cookieHeaders);
            string platformToken = GetTokenFromSetCookieHeader(cookieHeaders);
            Assert.NotNull(platformToken);
            ClaimsPrincipal claimPrincipal = JwtTokenMock.ValidateToken(platformToken);
            Assert.NotNull(claimPrincipal);
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
        }

        /// <summary>
        /// This test veryf the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component
        /// 3. OIDC is enabled and is the default authentication component
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        ///  In real life the ODIC provider will then authenticate user and redirect user back to authentication component
        ///  In this test the authorization code is created in test
        ///  5. User is redirectet back to authentication component with state and XSRF cookie
        ///  6. Authenticaiton component verifies XSRF header and cookie
        ///  7. Autentication component calls OIDC provider with code to exchange it to a token
        ///  8. Authentication compoment verifies token and create authentication info
        ///  9. Authentication component realizes it does not have a Altinn userId. Tryies to search for user based on external identity and the creates one Enriches the authentication info with userid and partyid
        ///  10. Authentication component creates altinn 3 token and puts it in to a cookie
        ///  11. Redirects back to original app
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCExternalIDentity_FullProcess_RedirectsToOIDCAndBackWithValidToken()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            UserProfile userProfileNotFound = null;
            UserProfile userProfile = new UserProfile { UserId = 234234, PartyId = 234234, UserName = "steph" };
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(userProfileNotFound);
            _userProfileService.Setup(u => u.CreateUser(It.IsAny<UserProfile>())).ReturnsAsync(userProfile);
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication?iss=uidp";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true&iss=uidp";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Call Authentication component equalt to browsr beeing redirect from Altinn app
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("uidp-qa.udir.no/connect/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify GoToCookie
            ValidateGoToCookie(redirectToOidcProviderResponse, HttpUtility.UrlEncode(gotoUrl));

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "asdf34argf", out string stateParam, out string nonceParam, out string redirectUriParam);

            // This part is the where we prepare the response as a OIDC Provider would do.
            // When returned from the OIDC Provider user will have an Authorization code. This code can have
            // different formats and this component does not need to understand it. It just exchanged code with
            // OIDC Provider to get a JWT ID token in response. In this test we create a JWTToken as code to make
            // the exchange in OidcProviderMock simple
            List<Claim> issClaims = new List<Claim>();
            issClaims.Add(new Claim("sub", "XAWED"));
            issClaims.Add(new Claim("urn:feide:role", "role1"));
            issClaims.Add(new Claim("urn:feide:role", "role2"));

            string authorizationCode = CreateOidcCode(null, null, nonceParam, issClaims);
            string redirectFromOidcProviderUri = GetAuthenticationUrlWithToken(redirectUriParam, stateParam, authorizationCode, null);
            HttpRequestMessage redirectFromOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, redirectFromOidcProviderUri);

            // Act 2. This simulates the request the browser will do when user is authenticated at OIDC provider and returns to Altinn authentication.
            HttpResponseMessage redirectFromOidcProviderResponse = await client.SendAsync(redirectFromOidcProviderRequest);

            // Assert: Now the user should be redirected back to original requested app.
            Assert.Equal(HttpStatusCode.Redirect, redirectFromOidcProviderResponse.StatusCode);
            Assert.StartsWith(gotoUrl, redirectFromOidcProviderResponse.Headers.Location.ToString());

            // Check to see if platform cookie is set with token and verify token and claims
            redirectFromOidcProviderResponse.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> cookieHeaders);
            Assert.NotEmpty(cookieHeaders);
            string platformToken = GetTokenFromSetCookieHeader(cookieHeaders);
            Assert.NotNull(platformToken);
            ClaimsPrincipal claimPrincipal = JwtTokenMock.ValidateToken(platformToken);
            Assert.NotNull(claimPrincipal);
            Assert.NotNull(claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("urn:altinn:userid")));
            Assert.Equal("234234",  claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("urn:altinn:userid")).Value);
            Assert.NotNull(claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("urn:feide:role")));
            Assert.Equal(2, claimPrincipal.Claims.Count(r => r.Type.Equals("urn:feide:role")));
        }

        /// <summary>
        /// This test veryf the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component
        /// 3. OIDC is enabled and is the default authentication component
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        ///  In real life the ODIC provider will then authenticate user and redirect user back to authentication component
        ///  In this test the authorization code is created in test
        ///  5. User is redirectet back to authentication component with state and XSRF cookie
        ///  6. Authenticaiton component verifies XSRF header and cookie
        ///  7. Autentication component calls OIDC provider with code to exchange it to a token
        ///  8. Authentication compoment verifies token and create authentication info
        ///  9. Authentication component realizes it does not have a Altinn userId. Tryies to search for user based on external identity and the creates one Enriches the authentication info with userid and partyid
        ///  10. Authentication component creates altinn 3 token and puts it in to a cookie
        ///  11. Redirects back to original app
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCExternalIDentityRelogin_FullProcess_RedirectsToOIDCAndBackWithValidToken()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";

            UserProfile userProfile = new UserProfile { UserId = 234235, PartyId = 234235, UserName = "steph" };
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(userProfile);
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication?iss=uidp";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true&iss=uidp";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Call Authentication component equalt to browsr beeing redirect from Altinn app
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("uidp-qa.udir.no/connect/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify GoToCookie
            ValidateGoToCookie(redirectToOidcProviderResponse, HttpUtility.UrlEncode(gotoUrl));

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "asdf34argf", out string stateParam, out string nonceParam, out string redirectUriParam);

            // This part is the where we prepare the response as a OIDC Provider would do.
            // When returned from the OIDC Provider user will have an Authorization code. This code can have
            // different formats and this component does not need to understand it. It just exchanged code with
            // OIDC Provider to get a JWT ID token in response. In this test we create a JWTToken as code to make
            // the exchange in OidcProviderMock simple
            List<Claim> issClaims = new List<Claim>();
            issClaims.Add(new Claim("sub", "2346t44663423s"));

            string authorizationCode = CreateOidcCode(null, null, nonceParam, issClaims);
            string redirectFromOidcProviderUri = GetAuthenticationUrlWithToken(redirectUriParam, stateParam, authorizationCode, null);
            HttpRequestMessage redirectFromOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, redirectFromOidcProviderUri);

            // Act 2. This simulates the request the browser will do when user is authenticated at OIDC provider and returns to Altinn authentication.
            HttpResponseMessage redirectFromOidcProviderResponse = await client.SendAsync(redirectFromOidcProviderRequest);

            // Assert: Now the user should be redirected back to original requested app.
            Assert.Equal(HttpStatusCode.Redirect, redirectFromOidcProviderResponse.StatusCode);
            Assert.StartsWith(gotoUrl, redirectFromOidcProviderResponse.Headers.Location.ToString());

            // Check to see if platform cookie is set with token and verify token and claims
            redirectFromOidcProviderResponse.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> cookieHeaders);
            Assert.NotEmpty(cookieHeaders);
            string platformToken = GetTokenFromSetCookieHeader(cookieHeaders);
            Assert.NotNull(platformToken);
            ClaimsPrincipal claimPrincipal = JwtTokenMock.ValidateToken(platformToken);
            Assert.NotNull(claimPrincipal);
            Assert.NotNull(claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("urn:altinn:userid")));
            Assert.Equal("234235", claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("urn:altinn:userid")).Value);
            Assert.Equal("2346t44663423s", claimPrincipal.Claims.FirstOrDefault(r => r.Type.Equals("sub")).Value);
        }

        /// <summary>
        /// This test veryf the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component
        /// 3. OIDC is enabled and is the default authentication component
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        ///  In real life the ODIC provider will then authenticate user and redirect user back to authentication component
        ///  In this test the authorization code is created in test
        ///  5. User is redirectet back to authentication component with state and XSRF cookie
        ///  6. Authenticaiton component verifies XSRF header and cookie and Nonce
        ///  Expection: Nonce is identifed as wrong an returns bad request
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDC_InvalidNonce_BadRequest()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Call Authentication component equalt to browsr beeing redirect from Altinn app
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idprovider.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "2314534634r2", out string stateParam, out string nonceParam, out string redirectUriParam);

            // This part is the where we prepare the response as a OIDC Provider would do.
            // When returned from the OIDC Provider user will have an Authorization code. This code can have
            // different formats and this component does not need to understand it. It just exchanged code with
            // OIDC Provider to get a JWT ID token in response. In this test we create a JWTToken as code to make
            // the exchange in OidcProviderMock simple
            string authorizationCode = CreateOidcCode("1337", "1337", "sdfsf");
            string redirectFromOidcProviderUri = GetAuthenticationUrlWithToken(redirectUriParam, stateParam, authorizationCode, "https://idprovider.azurewebsites.net/");
            HttpRequestMessage redirectFromOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, redirectFromOidcProviderUri);

            // Act 2. This simulates the request the browser will do when user is authenticated at OIDC provider and returns to Altinn authentication.
            HttpResponseMessage redirectFromOidcProviderResponse = await client.SendAsync(redirectFromOidcProviderRequest);

            // Assert: Now the user should get a bad request since nonce is wrong
            Assert.Equal(HttpStatusCode.BadRequest, redirectFromOidcProviderResponse.StatusCode);
        }

        /// <summary>
        /// This test veryf the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component
        /// 3. OIDC is enabled and is the default authentication component
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        ///  In real life the ODIC provider will then authenticate user and redirect user back to authentication component
        ///  In this test the authorization code is created in test
        ///  5. User is redirectet back to authentication component with state and XSRF cookie
        ///  6. Authenticaiton component verifies XSRF header and cookie and Nonce
        ///  Expection: state is identifed as wrong an returns bad request
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDC_InvalidState_BadRequest()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Call Authentication component equalt to browsr beeing redirect from Altinn app
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idprovider.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "2314534634r2", out string stateParam, out string nonceParam, out string redirectUriParam);

            // This part is the where we prepare the response as a OIDC Provider would do.
            // When returned from the OIDC Provider user will have an Authorization code. This code can have
            // different formats and this component does not need to understand it. It just exchanged code with
            // OIDC Provider to get a JWT ID token in response. In this test we create a JWTToken as code to make
            // the exchange in OidcProviderMock simple
            string authorizationCode = CreateOidcCode("1337", "1337", nonceParam);
            string redirectFromOidcProviderUri = GetAuthenticationUrlWithToken(redirectUriParam, "SFSDF", authorizationCode, "https://idprovider.azurewebsites.net/");
            HttpRequestMessage redirectFromOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, redirectFromOidcProviderUri);

            // Act 2. This simulates the request the browser will do when user is authenticated at OIDC provider and returns to Altinn authentication.
            HttpResponseMessage redirectFromOidcProviderResponse = await client.SendAsync(redirectFromOidcProviderRequest);

            // Assert: Now the user should get a bad request since state is wrong
            Assert.Equal(HttpStatusCode.BadRequest, redirectFromOidcProviderResponse.StatusCode);
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component with ISS given
        /// 3. OIDC is enabled and the default authentication mechanismen
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCEnabledAndDefault_IdportenProviderRequested_RedirectsToOIDCProvider()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true&iss=idporten";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Act
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idporten.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "345345s", out string stateParam, out string nonceParam, out string redirectUriParam);
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component with ISS given
        /// 3. OIDC is enabled and but not the default authentication mechanismen
        /// 4. First expections: Authentication components redirects to correct OIDC provider
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCEnabled_IdportenProviderRequested_RedirectsToOIDCProvider()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, true);
            string redirectUri = "http://localhost/authentication/api/v1/authentication";

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true&iss=idporten";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Act
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to correct Oidc provider and a XSRF Cookie was set
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToOidcProviderUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("idporten.azurewebsites.net/authorize", redirectToOidcProviderUri.Host + redirectToOidcProviderUri.AbsolutePath);

            // Verify that XSRF token cookie set is set. 
            ValidateXSRFTokenPresent(redirectToOidcProviderResponse);

            // Verify that all required OIDC Params are set and have the correct values
            ValidateOidcParams(redirectToOidcProviderUri, redirectUri, "345345s", out string stateParam, out string nonceParam, out string redirectUriParam);
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component with ISS given
        /// 3. OIDC is disabled
        /// Expections: Authentication components redirects to SBL, iss ignored
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCDisabled_IdportenProviderRequested_RedirectsToSBL()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, false, false);

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true&iss=idporten";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Act
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to SBL since OIDC is disabled
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToSBLUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("localhost/ui/authentication", redirectToSBLUri.Host + redirectToSBLUri.AbsolutePath);
        }

        /// <summary>
        /// This test verify the following Scenario
        /// 1. User tries to access app (http://ttd.apps.localhost/ttd/testapp)
        /// 2. User is redirected from app to authentication component 
        /// 3. OIDC is enabled
        /// Expections: Authentication components redirects to SBL since no ISS is given
        /// </summary>
        [Fact]
        public async Task AuthenticateUserWithOIDCEnabled_IdportenProviderNotRequested_RedirectsToSBL()
        {
            // Arrange         
            string gotoUrl = "http://ttd.apps.localhost/ttd/testapp";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, null, null, null, true, false);

            string url = "/authentication/api/v1/authentication?goto=" + HttpUtility.UrlEncode(gotoUrl) + "&DontChooseReportee=true";
            HttpRequestMessage redirectToOidcProviderRequest = new HttpRequestMessage(HttpMethod.Get, url);

            // Act
            HttpResponseMessage redirectToOidcProviderResponse = await client.SendAsync(redirectToOidcProviderRequest);

            // Assert that user is redirected to SBL since OIDC is disabled
            Assert.Equal(HttpStatusCode.Redirect, redirectToOidcProviderResponse.StatusCode);
            Uri redirectToSBLUri = new Uri(redirectToOidcProviderResponse.Headers.Location.ToString());
            Assert.Equal("localhost/ui/authentication", redirectToSBLUri.Host + redirectToSBLUri.AbsolutePath);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.AuthenticateUser"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateUser_RequestTokenWithValidAltinnCookie_SblBridgeUnavailable_ReturnsServiceUnavailable()
        {
            // Arrange
            HttpResponseMessage bridgeResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                ReasonPhrase = "Service Unavailable"
            };
            SblBridgeResponseException sblBridgeResponseException = new SblBridgeResponseException(bridgeResponse);
            _cookieDecryptionService.Setup(s => s.DecryptTicket(It.IsAny<string>())).ThrowsAsync(sblBridgeResponseException);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            string url = "/authentication/api/v1/authentication?goto=http%3A%2F%2Flocalhost";
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add("Cookie", ".ASPXAUTH=asdasdasd");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateExternalSystemToken_MissingBearerToken_NotAuthorized()
        {
            // Arrange
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);
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
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);
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
            string token = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);
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

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            UserProfile userProfile = new UserProfile { UserId = 20000, PartyId = 50001, UserName = "steph" };
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(userProfile);

            Mock<IEventsQueueClient> eventQueue = new Mock<IEventsQueueClient>();
            eventQueue.Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>()));

            AuthenticationEvent expectedAuthenticationEvent = GetAuthenticationEvent(AuthenticationMethod.MinIDPin, SecurityLevel.VerySensitive, null, AuthenticationEventType.TokenExchange, 20000);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object, eventQueue.Object, timeProviderMock.Object, guidService.Object);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);
            SecurityToken securityToken = JwtTokenMock.GetSecurityToken(token);
            SecurityToken securityTokenExternal = JwtTokenMock.GetSecurityToken(externalToken);

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:userid"));
            Assert.True(principal.HasClaim(c => c.Type == "pid"));
            Assert.Equal(expectedAuthLevel, principal.FindFirstValue("urn:altinn:authlevel"));
            Assert.Equal(securityTokenExternal.ValidTo, securityToken.ValidTo);
            AssertionUtil.AssertAuthenticationEvent(eventQueue, expectedAuthenticationEvent, Times.Once());
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

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            UserProfile userProfile = new UserProfile { UserId = 20000, PartyId = 50001, UserName = "steph" };
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(userProfile);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);
            SecurityToken securityToken = JwtTokenMock.GetSecurityToken(token);
            SecurityToken securityTokenExternal = JwtTokenMock.GetSecurityToken(externalToken);

            Assert.NotNull(principal);

            Assert.True(principal.HasClaim(c => c.Type == "urn:altinn:userid"));
            Assert.True(principal.HasClaim(c => c.Type == "pid"));
            Assert.Equal(expectedAuthLevel, principal.FindFirstValue("urn:altinn:authlevel"));
            Assert.Equal(securityTokenExternal.ValidTo, securityToken.ValidTo);
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

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

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

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).Throws(new Exception());

            string url = "/authentication/api/v1/exchange/id-porten";
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

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

            string externalToken = JwtTokenMock.GenerateEncryptedAndSignedToken(externalPrincipal, TimeSpan.FromMinutes(2));
            ClaimsPrincipal claimsPrincipal = JwtTokenMock.ValidateEncryptedAndSignedToken(externalToken);
            Assert.Equal(externalPrincipal.Identity.Name, claimsPrincipal.Identity.Name);
        }

        /// <summary>
        /// Test of method <see cref="AuthenticationController.ExchangeExternalSystemToken"/>.
        /// </summary>
        [Fact]
        public async Task AuthenticateStudioToken_ValidToken_ReturnsNewToken()
        {
            // Arrange
            string accessToken = JwtTokenMock.GenerateAccessToken("studio", "studio.designer", TimeSpan.FromMinutes(2));

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "/authentication/api/v1/exchange/altinnstudio");

            // Act
            HttpResponseMessage response = await client.SendAsync(requestMessage);

            // Assert
            string token = await response.Content.ReadAsStringAsync();

            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(token);

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
            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

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
            string expectedAuthLevel = "4";

            List<Claim> claims = new List<Claim>();

            string pid = "19108000239";
            string amr = "Minid-PIN";
            string acr = "idporten-loa-high";
            string jti = "BHqitIevJmeX_IrOzmS1XOvAQAWlrTK2OioLnx43Kqw";

            claims.Add(new Claim("pid", pid));
            claims.Add(new Claim("amr", amr));
            claims.Add(new Claim("acr", acr));
            claims.Add(new Claim("jti", jti));

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            UserProfile userProfile = new UserProfile { UserId = 20000, PartyId = 50001, UserName = "steph" };
            _userProfileService.Setup(u => u.GetUser(It.IsAny<string>())).ReturnsAsync(userProfile);

            HttpClient client = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", externalToken);
            string url = "/authentication/api/v1/exchange/id-porten";

            // Act
            HttpResponseMessage response = await client.GetAsync(url);

            // Get the altinn token
            string token = await response.Content.ReadAsStringAsync();
            ClaimsPrincipal altinnTokenPrincipal = JwtTokenMock.ValidateToken(token);
            string altinnSessionId = altinnTokenPrincipal.FindFirstValue("jti");

            url = "/authentication/api/v1/refresh";
            HttpClient refreshClient = GetTestClient(_cookieDecryptionService.Object, _userProfileService.Object);
            refreshClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage refreshedTokenMessage = await refreshClient.GetAsync(url);
            string refreshedToken = await refreshedTokenMessage.Content.ReadAsStringAsync();
            ClaimsPrincipal principal = JwtTokenMock.ValidateToken(refreshedToken);

            Assert.NotNull(principal);
            Assert.NotEqual(jti, principal.FindFirstValue("jti"));
            Assert.Equal(altinnSessionId, principal.FindFirstValue("jti"));
            Assert.True(principal.HasClaim(c => c.Type == "amr"));
        }

        private HttpClient GetTestClient(
            ISblCookieDecryptionService cookieDecryptionService, 
            IUserProfileService userProfileService, 
            IEventsQueueClient eventLog = null, 
            TimeProvider timeProviderMock = null,
            IGuidService guidService = null,
            bool enableOidc = false, 
            bool forceOidc = false, 
            string defaultOidc = "altinn")
        {
            HttpClient client = _factory.WithWebHostBuilder(builder =>
            {
                string configPath = GetConfigPath();
                builder.ConfigureAppConfiguration((context, conf) =>
                {
                    conf.AddJsonFile(configPath);
                });

                var configuration = new ConfigurationBuilder()
                  .AddJsonFile(configPath)
                  .Build();

                configuration.GetSection("GeneralSettings:EnableOidc").Value = enableOidc.ToString();
                configuration.GetSection("GeneralSettings:ForceOidc").Value = forceOidc.ToString();
                configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = defaultOidc;

                IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

                builder.ConfigureTestServices(services =>
                {
                    services.Configure<GeneralSettings>(generalSettingSection);
                    services.AddSingleton(cookieDecryptionService);
                    services.AddSingleton(userProfileService);
                    services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
                    services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
                    services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
                    services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
                    services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
                    if (eventLog != null)
                    {
                        services.AddSingleton(eventLog);
                    }

                    if (timeProviderMock != null)
                    {
                        services.AddSingleton(timeProviderMock);
                    }

                    if (guidService != null)
                    {
                        services.AddSingleton(guidService);
                    }
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            return client;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private static string GetAuthenticationUrlWithToken(string redirectUri, string state, string code, string iss)
        {
            if (!redirectUri.Contains('?'))
            {
                redirectUri += "?";
            }
            else
            {
                redirectUri += "&";
            }

            redirectUri += "state=" + state + "&code=" + code;
            if (!string.IsNullOrEmpty(iss))
            {
               redirectUri += "&iss=" + iss;
            }

            return redirectUri;
        }

        private static string CreateOidcCode(string userId, string partyId, string nonce, List<Claim> issClaims = null)
        {
            List<Claim> claims = new List<Claim>();

            if (!string.IsNullOrEmpty(userId))
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userId));
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, partyId));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, Enum.AuthenticationMethod.BankIDMobil.ToString()));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, Enum.SecurityLevel.VerySensitive.ToString()));
            }

            claims.Add(new Claim("nonce", nonce));
            if (issClaims != null)
            {
                claims.AddRange(issClaims);
            }

            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            return externalToken;
        }

        private static string GetTokenFromSetCookieHeader(IEnumerable<string> setCookieHeaders)
        {
            string platformToken = null;

            foreach (string cookiePart in setCookieHeaders)
            {
                string[] cookieKeyValue = cookiePart.Split('=');

                switch (cookieKeyValue[0])
                {
                    case "AltinnStudioRuntime":
                        platformToken = cookieKeyValue[1].Split("; ")[0];
                        break;
                }
            }

            return platformToken;
        }

        private static bool IsCookieSet(IEnumerable<string> setCookieHeaders, string cookieName)
        {
            bool cookieIsSet = false;
            foreach (string header in setCookieHeaders)
            {
                if (header.Contains(cookieName))
                {
                    cookieIsSet = true;
                }
            }

            return cookieIsSet;
        }

        private static bool HasCookieValue(IEnumerable<string> setCookieHeaders, string cookieName, string cookieValue)
        {
            bool cookieIsSet = false;
            foreach (string header in setCookieHeaders)
            {
                if (header.Contains(cookieName))
                {
                    cookieIsSet = true;
                    string cookieValueSet = header.Split(";")[0];
                    string cookieValueClean = cookieValueSet.Replace(cookieName + "=", string.Empty).Trim();
                    return cookieValueClean.ToLower().Equals(cookieValue.Trim().ToLower());
                }
            }

            return cookieIsSet;
        }

        private static void ValidateOidcParams(
            Uri redirectToOidcProviderUri,
            string expectedRedirectUri,
            string expectedClientId,
            out string stateParam,
            out string nonceParam,
            out string redirectUriParam)
        {
            System.Collections.Specialized.NameValueCollection queryDictionary = System.Web.HttpUtility.ParseQueryString(redirectToOidcProviderUri.Query);
            redirectUriParam = queryDictionary.Get("redirect_uri");
            stateParam = queryDictionary.Get("state");
            string scopeParam = queryDictionary.Get("scope");
            nonceParam = queryDictionary.Get("nonce");
            string responseTypeParam = queryDictionary.Get("response_type");
            string clientIdParam = queryDictionary.Get("client_id");
            Assert.NotNull(redirectUriParam);
            Assert.NotNull(stateParam); // This is autogenerated and not possible to know. Is valiated on next request
            Assert.NotNull(scopeParam);
            Assert.NotNull(nonceParam); // This is autogenerated and not possible to know.
            Assert.NotNull(responseTypeParam);
            Assert.NotNull(clientIdParam);
            Assert.Equal(HttpUtility.UrlDecode(expectedRedirectUri), redirectUriParam);
            Assert.Contains("openid", scopeParam);
            Assert.Equal("code", responseTypeParam);
            Assert.Equal(expectedClientId, clientIdParam); // Correct ID from the OIDC configuration for the given provider
        }

        private static void ValidateXSRFTokenPresent(HttpResponseMessage redirectToOidcProviderResponse)
        {
            redirectToOidcProviderResponse.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> setCookieHeaders);
            Assert.NotEmpty(setCookieHeaders);
            Assert.True(IsCookieSet(setCookieHeaders, "AS-XSRF-TOKEN"));
            Assert.True(IsCookieSet(setCookieHeaders, "oidcnonce"));
        }

        private static void ValidateGoToCookie(HttpResponseMessage redirectToOidcProviderResponse, string gotoUrl)
        {
            redirectToOidcProviderResponse.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string> setCookieHeaders);
            Assert.NotEmpty(setCookieHeaders);
            Assert.True(HasCookieValue(setCookieHeaders, "authngoto", gotoUrl));
        }

        private static AuthenticationEvent GetAuthenticationEvent(AuthenticationMethod authMethod, SecurityLevel authLevel, int? orgNumber, AuthenticationEventType authEventType, int? userId = null, bool isAuthenticated = true, string? externalSessionId = null)
        {
            AuthenticationEvent authenticationEvent = new AuthenticationEvent();
            authenticationEvent.Created = new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero);
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

        private void SetupDateTimeMock()
        {
            timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
        }

        private void SetupGuidMock()
        {
            guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
        }
    }
}
