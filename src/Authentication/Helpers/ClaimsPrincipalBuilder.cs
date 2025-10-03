using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    /// <summary>
    /// Helper that creates a <see cref="ClaimsPrincipal"/> from an <see cref="AuthCodeRow"/>.
    /// </summary>
    public static class ClaimsPrincipalBuilder
    {
        /// <summary>
        /// Baed on OidcBindingContextBase, creates a ClaimsPrincipal with relevant claims.
        /// </summary>
        public static ClaimsPrincipal GetClaimsPrincipal(OidcBindingContextBase oidcBindingContext, string iss, bool isIDToken = false)
        {
            List<Claim> claims = new()
            {
                new Claim("sub", oidcBindingContext.SubjectId),
                new Claim("sid", oidcBindingContext.SessionId),
                new Claim("iss", iss)
            };

            SecurityLevel securityLevel = SecurityLevel.SelfIdentifed;

            if (oidcBindingContext.SubjectPartyUuid != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, oidcBindingContext.SubjectPartyUuid.ToString()!));
            }

            if (oidcBindingContext.SubjectPartyId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, oidcBindingContext.SubjectPartyId.ToString()!, ClaimValueTypes.Integer64));
            }

            if (oidcBindingContext.SubjectUserId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, oidcBindingContext.SubjectUserId.ToString()!));
            }

            if (oidcBindingContext.SubjectId != null)
            {
                claims.Add(new Claim("pid", oidcBindingContext.SubjectId));
            }

            if (oidcBindingContext.Acr != null)
            {
                claims.Add(new Claim("acr", oidcBindingContext.Acr));
                securityLevel = GetAuthenticationLevelForIdPorten(oidcBindingContext.Acr);
            }

            int securityLevelValue = (int)securityLevel;
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, securityLevelValue.ToString(), ClaimValueTypes.Integer64));

            if (oidcBindingContext.Amr != null && oidcBindingContext.Amr.Count() != 0)
            {
                var amr = oidcBindingContext.Amr
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToArray();

                if (amr.Length > 0)
                {
                    string amrJson = JsonSerializer.Serialize(amr); // e.g. ["TestID","pwd"]
                    claims.Add(new Claim("amr", amrJson, JsonClaimValueTypes.JsonArray));
                }

                string amrClaim = AuthenticationHelper.GetAuthenticationMethod(string.Join(" ", oidcBindingContext.Amr)).ToString();
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, amrClaim));
            }

            if (oidcBindingContext.Nonce != null && isIDToken)
            {
                claims.Add(new Claim("nonce", oidcBindingContext.Nonce));
            }

            if (oidcBindingContext.AuthTime != null)
            {
                long authTimeEpoch = ((DateTimeOffset)oidcBindingContext.AuthTime).ToUnixTimeSeconds();
                claims.Add(new Claim("auth_time", authTimeEpoch.ToString(), ClaimValueTypes.Integer64));
            }

            if (!isIDToken && oidcBindingContext.Scopes != null && oidcBindingContext.Scopes.Any())
            {
                claims.Add(new Claim("scope", string.Join(" ", oidcBindingContext.Scopes)));
            }

            ClaimsIdentity identity = new(claims, "Token");
            ClaimsPrincipal principal = new(identity);

            return principal;
        }

        /// <summary>
        /// Create a ClaimsPrincipal based on an OidcSession for AltinnStudio runetime cookie
        /// </summary>
        public static ClaimsPrincipal GetClaimsPrincipal(OidcSession oidcSession, string iss, bool isIDToken = false)
        {
            List<Claim> claims = new()
            {
                new Claim("sub", oidcSession.SubjectId),
                new Claim("sid", oidcSession.Sid),
                new Claim("iss", iss)
            };

            SecurityLevel securityLevel = SecurityLevel.SelfIdentifed;

            if (oidcSession.SubjectPartyUuid != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, oidcSession.SubjectPartyUuid.ToString()!));
            }

            if (oidcSession.SubjectPartyId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, oidcSession.SubjectPartyId.ToString()!, ClaimValueTypes.Integer64));
            }

            if (oidcSession.SubjectUserId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, oidcSession.SubjectUserId.ToString()!));
            }

            if (oidcSession.SubjectId != null)
            {
                claims.Add(new Claim("pid", oidcSession.SubjectId));
            }

            if (oidcSession.Acr != null)
            {
                claims.Add(new Claim("acr", oidcSession.Acr));
                securityLevel = GetAuthenticationLevelForIdPorten(oidcSession.Acr);
            }

            int securityLevelValue = (int)securityLevel;
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, securityLevelValue.ToString(), ClaimValueTypes.Integer64));

            if (oidcSession.Amr != null && oidcSession.Amr.Count() != 0)
            {
                var amr = oidcSession.Amr
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToArray();

                if (amr.Length > 0)
                {
                    string amrJson = JsonSerializer.Serialize(amr); // e.g. ["TestID","pwd"]
                    claims.Add(new Claim("amr", amrJson, JsonClaimValueTypes.JsonArray));
                }

                string amrClaim = AuthenticationHelper.GetAuthenticationMethod(string.Join(" ", oidcSession.Amr)).ToString();
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, amrClaim));
            }
           
            if (oidcSession.AuthTime != null)
            {
                long authTimeEpoch = ((DateTimeOffset)oidcSession.AuthTime).ToUnixTimeSeconds();
                claims.Add(new Claim("auth_time", authTimeEpoch.ToString(), ClaimValueTypes.Integer64));
            }

            if (!isIDToken && oidcSession != null && oidcSession.Scopes.Any())
            {
                claims.Add(new Claim("scope", string.Join(" ", oidcSession.Scopes)));
            }

            ClaimsIdentity identity = new(claims, "Token");
            ClaimsPrincipal principal = new(identity);

            return principal;
        }

        private static SecurityLevel GetAuthenticationLevelForIdPorten(string acr)
        {
            switch (acr)
            {
                case "selfregistered-email":
                    return Enum.SecurityLevel.NotSensitive;
                case "idporten-loa-substantial":
                    return Enum.SecurityLevel.Sensitive;
                case "idporten-loa-high":
                    return Enum.SecurityLevel.VerySensitive;
                default:
                    return Enum.SecurityLevel.NotSensitive;
            }
        }
    }
}
