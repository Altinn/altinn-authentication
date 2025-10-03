using System.Security.Claims;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Models;
using AltinnCore.Authentication.Constants;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Helpers
{
    public static class TokenAssertsHelper
    {
        public static void AssertTokenResponse(TokenResponseDto tokenResponseDto, OidcTestScenario testScenario)
        {
            Assert.NotNull(tokenResponseDto);
            Assert.NotNull(tokenResponseDto.access_token);
            Assert.Equal("Bearer", tokenResponseDto.token_type);
            Assert.True(tokenResponseDto.expires_in > 0);
            Assert.NotNull(tokenResponseDto.id_token); // since openid scope was requested
            Assert.NotNull(tokenResponseDto.refresh_token);
            Assert.Contains("openid", tokenResponseDto.scope.Split(' '));
            Assert.Contains("altinn:portal/enduser", tokenResponseDto.scope.Split(' '));
            AssertAccessToken(tokenResponseDto.access_token, testScenario);
            AssertIdToken(tokenResponseDto.id_token, testScenario);
        }

        public static void AssertAccessToken(string accessToken, OidcTestScenario testScenario)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(accessToken);
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

        public static void AssertIdToken(string accessToken, OidcTestScenario testScenario)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(accessToken);
            Assert.NotNull(accessTokenPrincipal);
            Assert.NotNull(accessTokenPrincipal.Identity);
            Assert.NotEmpty(accessTokenPrincipal.Claims);
            Assert.True(accessTokenPrincipal.Identity.IsAuthenticated);
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "pid" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "acr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "amr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            Assert.DoesNotContain(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains("openid"));
        }
    }
}
