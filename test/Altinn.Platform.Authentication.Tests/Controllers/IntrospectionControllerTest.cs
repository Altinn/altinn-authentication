using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using AltinnCore.Authentication.Constants;
using AltinnCore.Authentication.JwtCookie;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    public class IntrospectionControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture) 
        : WebApplicationTests(dbFixture,  webApplicationFixture)
    {
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
        private readonly Mock<IEFormidlingAccessValidator> _eformidlingValidatorService = new();
        private readonly string _baseUrl = "/authentication/api/v1/introspection";
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddSingleton<IEFormidlingAccessValidator>(_eformidlingValidatorService.Object);
            services.AddSingleton<IAuthentication, AuthenticationCore>();            
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        }

        /// <summary>
        /// Scenario : Endpoint called with token hint `eFormidlingAccessToken`
        /// Expected : EFormidling validator service's validate method is called. 
        /// Success Result: 200 status code, and response object from service returned.
        /// </summary>
        [Fact]
        public async Task ValidateToken_TokenHintEFormidling_EFormidlingServiceCalled()
        {
            // Arrange
            IntrospectionResponse expected = new()
            {
                Active = true,
                Iss = "digdir"
            };

            _eformidlingValidatorService.Setup(efvs => efvs.ValidateToken(It.IsAny<string>())).ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Iss = "digdir"
            });

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "token", "thisIsMyRandomToken" },
                    { "token_type_hint", "eFormidlingAccessToken" }
                }),
            };

            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            HttpClient client = CreateClient();

            string token = JwtTokenMock.GenerateToken(GetTestPrincipal(), TimeSpan.FromMinutes(2));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            HttpResponseMessage res = await client.SendAsync(requestMessage);
            string responseString = await res.Content.ReadAsStringAsync();
            IntrospectionResponse actual = JsonSerializer.Deserialize<IntrospectionResponse>(responseString, _options);

            // Assert
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            AdvancedAsserts.Equal(expected, actual);
            _eformidlingValidatorService.Verify(efvs => efvs.ValidateToken(It.IsAny<string>()), Times.Once());
        }

        /// <summary>
        /// Scenario : Endpoint called without a token hint and with an invalid token.
        /// Expected : All available validators are called until a match is met, or all have been tested. 
        /// Success Result: 200 status code, and a false active response is returned.
        /// </summary>
        [Fact]
        public async Task ValidateToken_TokenHintNone_EFormidlingServiceCalled()
        {
            // Arrange        
            _eformidlingValidatorService.Setup(efvs => efvs.ValidateToken(It.IsAny<string>())).ReturnsAsync(new IntrospectionResponse
            {
                Active = false,
            });

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "token", "thisIsMyRandomToken" },
                }),
            };

            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            HttpClient client = CreateClient();

            string token = JwtTokenMock.GenerateToken(GetTestPrincipal(), TimeSpan.FromMinutes(2));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            HttpResponseMessage res = await client.SendAsync(requestMessage);
            string responseString = await res.Content.ReadAsStringAsync();
            IntrospectionResponse actual = JsonSerializer.Deserialize<IntrospectionResponse>(responseString, _options);

            // Assert
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.False(actual.Active);
            _eformidlingValidatorService.Verify(efvs => efvs.ValidateToken(It.IsAny<string>()), Times.Once());
        }

        /// <summary>
        /// Scenario : Endpoint called without a token hint and with an invalid token.
        /// Expected : All available validators are called until a match is met, or all have been tested. 
        /// Success Result: 401 status code. 
        /// </summary>
        [Fact]
        public async Task ValidateToken_NoBearerToken_Unauthorized()
        {
            // Arrange                 
            HttpClient client = CreateClient();

            // Act
            HttpResponseMessage res = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, _baseUrl));

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        private ClaimsPrincipal GetTestPrincipal()
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "1337", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserId, "1337", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, "1337", ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "2", ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            return new ClaimsPrincipal(identity);
        }
    }
}