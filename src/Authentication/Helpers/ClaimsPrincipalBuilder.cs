using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Urn;
using AltinnCore.Authentication.Constants;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    /// <summary>
    /// Helper that creates <see cref="ClaimsPrincipal"/>
    /// </summary>
    public static class ClaimsPrincipalBuilder
    {
        private const string OriginalIssClaimName = "originaliss";

        /// <summary>
        /// Based on OidcBindingContextBase, creates a ClaimsPrincipal with relevant claims.
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

            if (oidcBindingContext.SubjectUserName != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserName, oidcBindingContext.SubjectUserName));
            }

            if (oidcBindingContext.ExternalId != null && oidcBindingContext.ExternalId.StartsWith(AltinnCoreClaimTypes.PersonIdentifier))
            {
                claims.Add(new Claim("pid", oidcBindingContext.ExternalId.Replace($"{AltinnCoreClaimTypes.PersonIdentifier}:", string.Empty)));
            }
            else if (oidcBindingContext.ExternalId != null && !oidcBindingContext.ExternalId.StartsWith(AltinnCoreClaimTypes.IdPortenEmailPrefix))
            {
                claims.Add(new Claim("orgsub", oidcBindingContext.ExternalId));
            }

            if (oidcBindingContext.ExternalId != null && oidcBindingContext.ExternalId.StartsWith(AltinnCoreClaimTypes.IdPortenEmailPrefix))
            {
                UrnEncoded.TryUnescape(oidcBindingContext.ExternalId.AsSpan(AltinnCoreClaimTypes.IdPortenEmailPrefix.Length + 1), out var emailEncoded);
                claims.Add(new Claim(AltinnCoreClaimTypes.Email, emailEncoded.Value));
                claims.Add(new Claim(AltinnCoreClaimTypes.ExternalIdentifer, oidcBindingContext.ExternalId));
            }
         
            if (oidcBindingContext.Acr != null)
            {
                claims.Add(new Claim("acr", oidcBindingContext.Acr));
                securityLevel = AuthenticationHelper.GetAuthenticationLevelForIdPorten(oidcBindingContext.Acr);
            }

            if (oidcBindingContext.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in oidcBindingContext.ProviderClaims)
                {
                    foreach (string claimValue in kvp.Value)
                    {
                        claims.Add(new Claim(kvp.Key, claimValue));
                    }
                }
            }

            int securityLevelValue = (int)securityLevel;
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, securityLevelValue.ToString(), ClaimValueTypes.Integer64));

            if (oidcBindingContext.Amr != null && oidcBindingContext.Amr.Length > 0)
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
                    string amrClaim = AuthenticationHelper.GetAuthenticationMethod(amr[0]).ToString();
                    claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, amrClaim));
                }
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

            if (!isIDToken && oidcBindingContext.Scopes != null && oidcBindingContext.Scopes.Length > 0)
            {
                claims.Add(new Claim("scope", string.Join(" ", oidcBindingContext.Scopes)));
            }

            ClaimsIdentity identity = new(claims, "Token");
            ClaimsPrincipal principal = new(identity);

            return principal;
        }

        /// <summary>
        /// Create a ClaimsPrincipal based on an OidcSession for AltinnStudio runtime cookie
        /// </summary>
        public static ClaimsPrincipal GetClaimsPrincipal(OidcSession oidcSession, string iss, bool isIDToken = false, bool isAuthCookie = false)
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

            if (isAuthCookie)
            {
                claims.Add(new Claim("jti", oidcSession.Sid));
            }

            if (oidcSession.SubjectPartyId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, oidcSession.SubjectPartyId.ToString()!, ClaimValueTypes.Integer64));
            }

            if (oidcSession.SubjectUserId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, oidcSession.SubjectUserId.ToString()!));
            }

            if (!isAuthCookie && oidcSession.ExternalId != null && oidcSession.ExternalId.StartsWith(AltinnCoreClaimTypes.PersonIdentifier))
            {
                claims.Add(new Claim("pid", oidcSession.ExternalId.Replace($"{AltinnCoreClaimTypes.PersonIdentifier}:", string.Empty)));
            }

            if (oidcSession.SubjectUserName != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserName, oidcSession.SubjectUserName, string.Empty));
            }

            if (isAuthCookie && oidcSession.Provider != null)  
            {
                claims.Add(new Claim(OriginalIssClaimName, oidcSession.Provider));
            }

            if (oidcSession.Acr != null)
            {
                claims.Add(new Claim("acr", oidcSession.Acr));
                securityLevel = AuthenticationHelper.GetAuthenticationLevelForIdPorten(oidcSession.Acr);
            }

            if (oidcSession.ProviderClaims != null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in oidcSession.ProviderClaims)
                {
                    foreach (string claimValue in kvp.Value)
                    {
                        claims.Add(new Claim(kvp.Key, claimValue));
                    }
                }
            }

            int securityLevelValue = (int)securityLevel;
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, securityLevelValue.ToString(), ClaimValueTypes.Integer64));

            if (oidcSession.Amr != null && oidcSession.Amr.Length > 0)
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
                    string amrClaim = AuthenticationHelper.GetAuthenticationMethod(amr[0]).ToString();
                    claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, amrClaim));
                }
            }
           
            if (oidcSession.AuthTime != null)
            {
                long authTimeEpoch = ((DateTimeOffset)oidcSession.AuthTime).ToUnixTimeSeconds();
                claims.Add(new Claim("auth_time", authTimeEpoch.ToString(), ClaimValueTypes.Integer64));
            }

            if (!isIDToken && oidcSession != null && oidcSession.Scopes.Length > 0)
            {
                claims.Add(new Claim("scope", string.Join(" ", oidcSession.Scopes)));
            }

            ClaimsIdentity identity = new(claims, "Token");
            ClaimsPrincipal principal = new(identity);

            return principal;
        }
    }
}
