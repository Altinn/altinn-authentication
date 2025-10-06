using System;
using System.Linq;
using System.Security.Claims;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Models;
using AltinnCore.Authentication.Constants;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Helpers
{
    public static class TokenAssertsHelper
    {
        public static string AssertTokenResponse(TokenResponseDto tokenResponseDto, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.NotNull(tokenResponseDto);
            Assert.NotNull(tokenResponseDto.access_token);
            Assert.Equal("Bearer", tokenResponseDto.token_type);
            Assert.True(tokenResponseDto.expires_in > 0);
            Assert.NotNull(tokenResponseDto.id_token); // since openid scope was requested
            Assert.NotNull(tokenResponseDto.refresh_token);
            Assert.Contains("openid", tokenResponseDto.scope.Split(' '));
            Assert.Contains("altinn:portal/enduser", tokenResponseDto.scope.Split(' '));
            AssertAccessToken(tokenResponseDto.access_token, testScenario, now);
            string sid = AssertIdToken(tokenResponseDto.id_token, testScenario, now);
            return sid;
        }

        public static string AssertTokenRefreshResponse(TokenResponseDto tokenResponseDto, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.NotNull(tokenResponseDto);
            Assert.NotNull(tokenResponseDto.access_token);
            Assert.Equal("Bearer", tokenResponseDto.token_type);
            Assert.True(tokenResponseDto.expires_in > 0);
            Assert.NotNull(tokenResponseDto.id_token); // since openid scope was requested
            Assert.Contains("openid", tokenResponseDto.scope.Split(' '));
            Assert.Contains("altinn:portal/enduser", tokenResponseDto.scope.Split(' '));
            AssertAccessToken(tokenResponseDto.access_token, testScenario, now);
            string sid = AssertIdToken(tokenResponseDto.id_token, testScenario, now);
            return sid;
        }

        public static void AssertAccessToken(string accessToken, OidcTestScenario testScenario, DateTimeOffset now)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(accessToken, now);
  
            Assert.NotNull(accessTokenPrincipal);
            Assert.NotNull(accessTokenPrincipal.Identity);
            Assert.NotEmpty(accessTokenPrincipal.Claims);
            Assert.True(accessTokenPrincipal.Identity.IsAuthenticated);
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "pid" && c.Value.Equals(testScenario.Ssn));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));
 
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains(scope));
            }
        }

        public static string AssertIdToken(string accessToken, OidcTestScenario testScenario, DateTimeOffset now)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(accessToken, now);
            Assert.NotNull(accessTokenPrincipal);
            Assert.NotNull(accessTokenPrincipal.Identity);
            Assert.NotEmpty(accessTokenPrincipal.Claims);
            Assert.True(accessTokenPrincipal.Identity.IsAuthenticated);
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "pid" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));
            string sid = accessTokenPrincipal.Claims.First(c => c.Type == "sid").Value;
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "acr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "amr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            Assert.DoesNotContain(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains("openid"));

            return sid;
        }

        public static void AssertCookieAccessToken(string cookieToken, OidcTestScenario testScenario, DateTimeOffset now)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(cookieToken, now);

            Assert.NotNull(accessTokenPrincipal);
            Assert.NotNull(accessTokenPrincipal.Identity);
            Assert.NotEmpty(accessTokenPrincipal.Claims);
            Assert.True(accessTokenPrincipal.Identity.IsAuthenticated);

            // TODO: Is there any reason not to add issuer when creating cookie token?
            // Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && !string.IsNullOrEmpty(c.Value));
            // Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "pid" && c.Value.Equals(testScenario.Ssn));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));

            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains(scope));
            }
        }
    }
}
