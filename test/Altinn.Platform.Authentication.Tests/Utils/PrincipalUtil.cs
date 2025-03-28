using System;
using System.Collections.Generic;
using System.Security.Claims;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Tests;
using AltinnCore.Authentication.Constants;

namespace App.IntegrationTests.Utils
{
    public static class PrincipalUtil
    {
        public static readonly string AltinnCoreClaimTypesOrg = "urn:altinn:org";

        public static string GetToken(int userId, List<Claim> claims, int authenticationLevel = 2, bool addPortalScope = false)
        {
            ClaimsPrincipal principal = GetUserPrincipal(userId, claims, authenticationLevel, addPortalScope);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static ClaimsPrincipal GetUserPrincipal(int userId, List<Claim> extClaims, int authenticationLevel = 2, bool addPortalScope = false)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserName, "UserOne", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, userId.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "AltinnPIN", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer));
            
            if (addPortalScope)
            {
                claims.Add(new(AuthzConstants.CLAIM_SCOPE, "altinn:portal/enduser", ClaimValueTypes.String, issuer));
            }

            if (extClaims != null)
            {
                claims.AddRange(extClaims);
            }

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            return principal;
        }

        public static string GetOrgToken(string org, int authenticationLevel = 3)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Buypass", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

            return token;
        }

        public static string GetSelfIdentifiedUserToken(
            string username, string partyId,  string userId)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserName, username, ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, partyId.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "SelfIdentified", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "0", ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

            return token;
        }

        public static string GetOrgToken(string org, string orgNo, int authenticationLevel = 4)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.altinn.no";
            claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.OrgNumber, orgNo, ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "BankID", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

            return token;
        }

        public static string GetOrgToken(string org, string orgNumber = "991825827", string? scope = null, string[]? prefixes = null)
        {
            ClaimsPrincipal principal = GetClaimsPrincipal(org, orgNumber, scope, prefixes);

            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

            return token;
        }

        public static ClaimsPrincipal GetClaimsPrincipal(string org, string orgNumber, string? scope = null, string[]? prefixes = null)
        {
            string issuer = "www.altinn.no";

            List<Claim> claims = new List<Claim>();
            if (!string.IsNullOrEmpty(org))
            {
                claims.Add(new Claim(AltinnCoreClaimTypesOrg, org, ClaimValueTypes.String, issuer));
            }

            if (scope != null)
            {
                claims.Add(new Claim("scope", scope, ClaimValueTypes.String, "maskinporten"));
            }

            if (prefixes is { Length: > 0 })
            {
                foreach (string prefix in prefixes)
                {
                    claims.Add(new Claim("consumer_prefix", prefix, ClaimValueTypes.String, "maskinporten"));
                }
            }

            claims.Add(new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim("consumer", GetOrgNoObject(orgNumber)));

            ClaimsIdentity identity = new ClaimsIdentity("mock-org");
            identity.AddClaims(claims);

            return new ClaimsPrincipal(identity);
        }

        private static string GetOrgNoObject(string orgNo)
        {
            return $"{{ \"authority\":\"iso6523-actorid-upis\", \"ID\":\"0192:{orgNo}\"}}";
        }
    }
}
