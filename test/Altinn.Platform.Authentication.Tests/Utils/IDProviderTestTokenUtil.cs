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
        public static OidcCodeResponse GetIdPortenTokenResponse(string pid, string email, string nonce, string sid, string[] acr, string[] amr, string client_id, string[] scope, DateTimeOffset auth_time)
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
                IdToken = GetIdPortenIDToken(sub, pid, email, locale, nonce, sid, client_id, acr, amr, auth_time),
                AccessToken = GetIdPortenAccessToken(sub, "unspecified", acr, client_id, "virksomhetssertifikat", digDirOrgNo, scope, pid, email, auth_time)
            };
            return response;
        }

        public static string GetIdPortenIDToken(string sub, string pid, string email, string locale, string nonce, string sid, string aud, string[] acr, string[] amr, DateTimeOffset auth_time)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.idporten.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            if (!string.IsNullOrEmpty(email))
            {
                claims.Add(new Claim("email", email, ClaimValueTypes.String, issuer));
            }

            if (!string.IsNullOrEmpty(pid))
            {
                claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            }

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

                if (amrr.Length > 0)
                {
                    string amrJson = JsonSerializer.Serialize(amrr); // e.g. ["TestID","pwd"]
                    claims.Add(new Claim("amr", amrJson, JsonClaimValueTypes.JsonArray));
                }
            }

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static string GetIdPortenAccessToken(string sub, string aud, string[] acr, string client_id, string client_amr, string consumer, string[] scope, string pid, string email, DateTimeOffset iat)
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
            if (!string.IsNullOrEmpty(email))
            {
                claims.Add(new Claim("email", email, ClaimValueTypes.String, issuer));
            }

            if (!string.IsNullOrEmpty(pid))
            {
                claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            }

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
            claims.Add(new Claim("sub", scenario.ExternalIdentity!, ClaimValueTypes.String, issuer)); // scenario test data always sets ExternalIdentity
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
            claims.Add(new Claim("sub", scenario.ExternalIdentity!, ClaimValueTypes.String, issuer)); // scenario test data always sets ExternalIdentity
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

        /// <summary>
        /// Builds a HelseID-shaped token response. HelseID does not follow ID-porten's claim
        /// conventions, and this token deliberately reproduces every one of those differences:
        /// no <c>acr</c> claim at all, the level in <c>helseid://claims/identity/security_level</c>
        /// as the string "4", the national identity number under
        /// <c>helseid://claims/identity/pid</c>, and an <c>amr</c> of <c>["pwd"]</c> — the Duende
        /// default, which says nothing about the eID actually used. The real eID is in <c>idp</c>.
        /// </summary>
        public static OidcCodeResponse GetHelseIdTokenResponse(
            OidcTestScenario scenario,
            UpstreamLoginTransaction createdUpstreamLogingTransaction,
            string upstreamSid,
            DateTimeOffset authTime,
            string securityLevel = "4",
            string idp = "bankid-oidc",
            bool includePid = true)
        {
            string sub = Guid.NewGuid().ToString();

            return new OidcCodeResponse
            {
                TokenType = "bearer",
                ExpiresIn = 600,
                RefreshToken = "dummy-refresh",
                RefreshTokenExpiresIn = 600,
                Scope = string.Join(' ', createdUpstreamLogingTransaction.Scopes),
                IdToken = GetHelseIdIdToken(scenario, createdUpstreamLogingTransaction, sub, upstreamSid, authTime, securityLevel, idp, includePid),
                AccessToken = GetHelseIdAccessToken(scenario, createdUpstreamLogingTransaction, sub, authTime),
            };
        }

        public static string GetHelseIdIdToken(
            OidcTestScenario scenario,
            UpstreamLoginTransaction createdUpstreamLogingTransaction,
            string sub,
            string upstreamSid,
            DateTimeOffset authTime,
            string securityLevel,
            string idp,
            bool includePid)
        {
            // Must match the Issuer configured for the 'helseid' provider; the upstream token
            // validator rejects anything else.
            string issuer = "https://helseid-sts.test.nhn.no";

            List<Claim> claims =
            [
                new Claim("iss", issuer, ClaimValueTypes.String, issuer),
                new Claim("sub", sub, ClaimValueTypes.String, issuer),
                new Claim("aud", createdUpstreamLogingTransaction.UpstreamClientId, ClaimValueTypes.String, issuer),
                new Claim("nonce", createdUpstreamLogingTransaction.Nonce, ClaimValueTypes.String, issuer),
                new Claim("sid", upstreamSid, ClaimValueTypes.String, issuer),
                new Claim("auth_time", authTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer),

                // Deliberately NOT 'acr'. HelseID sends no acr claim.
                new Claim("helseid://claims/identity/security_level", securityLevel, ClaimValueTypes.String, issuer),

                // The Duende default. Carries no information about which eID was used, which is
                // exactly why the method has to be read from 'idp' instead.
                new Claim("amr", "pwd", ClaimValueTypes.String, issuer),
                new Claim("idp", idp, ClaimValueTypes.String, issuer),
            ];

            if (includePid && !string.IsNullOrEmpty(scenario.Ssn))
            {
                claims.Add(new Claim("helseid://claims/identity/pid", scenario.Ssn, ClaimValueTypes.String, issuer));
            }

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
            return JwtTokenMock.GenerateToken(new ClaimsPrincipal(identity), new TimeSpan(1, 1, 1));
        }

        public static string GetHelseIdAccessToken(
            OidcTestScenario scenario,
            UpstreamLoginTransaction createdUpstreamLogingTransaction,
            string sub,
            DateTimeOffset authTime)
        {
            string issuer = "https://helseid-sts.test.nhn.no";

            List<Claim> claims =
            [
                new Claim("iss", issuer, ClaimValueTypes.String, issuer),
                new Claim("sub", sub, ClaimValueTypes.String, issuer),
                new Claim("scope", string.Join(' ', createdUpstreamLogingTransaction.Scopes), ClaimValueTypes.String, issuer),
                new Claim("auth_time", authTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer),
            ];

            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            return JwtTokenMock.GenerateToken(new ClaimsPrincipal(identity), new TimeSpan(1, 1, 1));
        }
    }
}
