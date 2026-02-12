#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Tests.Models;
using Altinn.Urn;
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
            Assert.True(tokenResponseDto.expires_in == 600);
            Assert.NotNull(tokenResponseDto.id_token); // since openid scope was requested
            Assert.NotNull(tokenResponseDto.refresh_token);
            Assert.True(IsBase64Url(tokenResponseDto.refresh_token!), "refresh_token must be base64url");
            Assert.True(tokenResponseDto.refresh_token_expires_in == 1800);

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
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && c.Value.Equals("http://localhost/authentication/api/v1/openid/"));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            if (!testScenario.Acr.Contains("level0") && !testScenario.Acr.Contains("selfregistered-email"))
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "pid" && c.Value.Equals(testScenario.Ssn));
            }

            if (testScenario.Acr.Contains("selfregistered-email"))
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.AuthenticateMethod && c.Value.Equals(AuthenticationMethod.IdportenEpost.ToString()));
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.Email && c.Value.Equals(testScenario.Email));
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.ExternalIdentifier && c.Value.Equals(AltinnCoreClaimTypes.IdPortenEmailPrefix + ":" + UrnEncoded.Create(testScenario.Email!).Encoded));
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "acr" && c.Value.Equals(AuthzConstants.CLAIM_ACR_IDPORTEN_EMAIL));
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "amr" && c.Value.Equals(AuthzConstants.CLAIM_AMR_IDPORTEN_EMAIL));
            }

            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));

            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains(scope));
            }

            if (testScenario.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in testScenario.ProviderClaims)
                {
                    Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == kvp.Key && !string.IsNullOrEmpty(c.Value));
                }
            }
        }

        public static string AssertIdToken(string idToken, OidcTestScenario testScenario, DateTimeOffset now)
        {
            ClaimsPrincipal idTokenPrincipal = JwtTokenMock.ValidateToken(idToken, now);
            Assert.NotNull(idTokenPrincipal);
            Assert.NotNull(idTokenPrincipal.Identity);
            Assert.NotEmpty(idTokenPrincipal.Claims);
            Assert.True(idTokenPrincipal.Identity.IsAuthenticated);
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "iss" && c.Value.Equals("http://localhost/authentication/api/v1/openid/"));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "aud" && !string.IsNullOrEmpty(c.Value) && c.Value == testScenario.DownstreamClientId);
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.AuthenticateMethod && !string.IsNullOrEmpty(c.Value));
            string method = idTokenPrincipal.Claims.First(c => c.Type == AltinnCoreClaimTypes.AuthenticateMethod).Value;
            if (!method.Equals(AuthenticationMethod.SelfIdentified.ToString()) && !method.Equals(AuthenticationMethod.IdportenEpost.ToString()))
            {
                Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "pid" && !string.IsNullOrEmpty(c.Value));
            }

            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));
            string sid = idTokenPrincipal.Claims.First(c => c.Type == "sid").Value;
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "acr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == "amr" && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(idTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            Assert.DoesNotContain(idTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains("openid"));

            return sid;
        }

        public static string AssertCookieAccessToken(string cookieToken, OidcTestScenario testScenario, DateTimeOffset now)
        {
            ClaimsPrincipal accessTokenPrincipal = JwtTokenMock.ValidateToken(cookieToken, now);

            Assert.NotNull(accessTokenPrincipal);
            Assert.NotNull(accessTokenPrincipal.Identity);
            Assert.NotEmpty(accessTokenPrincipal.Claims);
            Assert.True(accessTokenPrincipal.Identity.IsAuthenticated);

            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "iss" && c.Value.Equals("http://localhost/authentication/api/v1/openid/"));

            // The 'sub' claim is not included in the cookie token for privacy reasons. TODO verify if this is still the case
            // Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
            Assert.DoesNotContain(accessTokenPrincipal.Claims, c => c.Type == "pid");
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "sid" && !string.IsNullOrEmpty(c.Value));
            string sid = accessTokenPrincipal.Claims.First(c => c.Type == "sid").Value;
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.PartyUUID && !string.IsNullOrEmpty(c.Value));
            Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == AltinnCoreClaimTypes.UserId && !string.IsNullOrEmpty(c.Value));
            foreach (string scope in testScenario.Scopes)
            {
                Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == "scope" && c.Value.Contains(scope));
            }

            if (testScenario.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in testScenario.ProviderClaims)
                {
                    Assert.Contains(accessTokenPrincipal.Claims, c => c.Type == kvp.Key && !string.IsNullOrEmpty(c.Value));
                }
            }

            return sid;
        }

        private static bool IsBase64Url(string s) =>
               s.All(c =>
                   (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9') ||
                   c == '-' || c == '_');
    }
}
