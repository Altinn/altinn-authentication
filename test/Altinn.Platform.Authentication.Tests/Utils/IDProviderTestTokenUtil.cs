using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Tests.Models;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class IDProviderTestTokenUtil
    {
        public static OidcCodeResponse GetIdPortenTokenResponse(string pid, string nonce, string sid, string[] acr, string[] amr, string client_id, string[] scope, DateTimeOffset auth_time)
        {
            string sub = Guid.NewGuid().ToString();
            string locale = "nb";
            string digDirOrgNo = "991825827";

            OidcCodeResponse response = new()
            {
                TokenType = "bearer",
                ExpiresIn = 600,
                RefreshToken = "dummy-refresh",
                RefreshTokenExpiresIn = 600,
                Scope = string.Join(' ', scope),
                IdToken = GetIdPortenIDToken(sub, pid, locale, nonce, sid, client_id, acr, amr, auth_time),
                AccessToken = GetIdPortenAccessToken(sub, "unspecificed", acr, client_id, "virksomhetssertifikat", digDirOrgNo, scope, pid, auth_time)
            };
            return response;
        }

        public static string GetIdPortenIDToken(string sub, string pid, string locale, string nonce, string sid, string aud, string[] acr, string[] amr, DateTimeOffset auth_time)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.idporten.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("locale", locale, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("nonce", nonce, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sid", sid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("aud", aud, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("acr", string.Join(" ", acr), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("auth_time", auth_time.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer));
            if (amr != null && amr.Length != 0)
            {
                var amrr = amr
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToArray();

                if (amr.Length > 0)
                {
                    string amrJson = JsonSerializer.Serialize(amr); // e.g. ["TestID","pwd"]
                    claims.Add(new Claim("amr", amrJson, JsonClaimValueTypes.JsonArray));
                }
            }

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static string GetIdPortenAccessToken(string sub, string aud, string[] acr, string client_id, string client_amr, string consumer, string[] scope, string pid, DateTimeOffset iat)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.idporten.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("aud", aud, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("acr", string.Join(" ", acr), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("client_id", client_id, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("client_amr", client_amr, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("consumer", consumer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("scope", string.Join(' ', scope), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("iat", iat.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer));
            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static OidcCodeResponse GetUidpTokenResponse(OidcTestScenario scenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            string sub = Guid.NewGuid().ToString();
            string locale = "nb";
            string digDirOrgNo = "991825827";

            OidcCodeResponse response = new()
            {
                TokenType = "bearer",
                ExpiresIn = 600,
                RefreshToken = "dummy-refresh",
                RefreshTokenExpiresIn = 600,
                Scope = string.Join(' ', createdUpstreamLogingTransaction.Scopes),
                IdToken = GetUidpIdToken(scenario, createdUpstreamLogingTransaction, authTime),
                AccessToken = GetUidpAccessToken(scenario, createdUpstreamLogingTransaction, authTime)
            };
            return response;
        }

        public static string GetUidpIdToken(OidcTestScenario scenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            Guid upstreamSID = Guid.NewGuid();
            List<Claim> claims = [];
            string issuer = "uidp.udir.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", scenario.ExternalIdentity, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("scope", string.Join(' ', createdUpstreamLogingTransaction.Scopes), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("nonce", createdUpstreamLogingTransaction.Nonce, ClaimValueTypes.String, issuer));

            if (scenario.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in scenario.ProviderClaims)
                {
                    foreach (string claimValue in kvp.Value)
                    {
                        claims.Add(new Claim(kvp.Key, claimValue, ClaimValueTypes.String, issuer));
                    }
                }
            }

            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static string GetUidpAccessToken(OidcTestScenario scenario, UpstreamLoginTransaction createdUpstreamLogingTransaction, DateTimeOffset authTime)
        {
            List<Claim> claims = [];
            string issuer = "uidp.udir.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", scenario.ExternalIdentity, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("scope", string.Join(' ', createdUpstreamLogingTransaction.Scopes), ClaimValueTypes.String, issuer));

            if (scenario.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in scenario.ProviderClaims)
                {
                    foreach (string claimValue in kvp.Value)
                    {
                        claims.Add(new Claim(kvp.Key, claimValue, ClaimValueTypes.String, issuer));
                    }
                }
            }

            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }
    }
}
