using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper class for authentication process
    /// </summary>
    public static class AuthenticationHelper
    {
        /// <summary>
        /// Get user information from the token
        /// </summary>
        /// <param name="jwtSecurityToken">jwt token</param>
        /// <param name="provider">authentication provider</param>
        /// <returns>user information</returns>
        public static UserAuthenticationModel GetUserFromToken(JwtSecurityToken jwtSecurityToken, OidcProvider provider)
        {
            UserAuthenticationModel userAuthenticationModel = new UserAuthenticationModel()
            {
                IsAuthenticated = true,
                ProviderClaims = new Dictionary<string, List<string>>(),
                Iss = provider?.IssuerKey,
                AuthenticationMethod = AuthenticationMethod.NotDefined
            };

            if (jwtSecurityToken != null)
            {
                foreach (Claim claim in jwtSecurityToken.Claims)
                {
                    // Handle various claim types
                    switch (claim.Type)
                    {
                        // General OIDC claims
                        case "nonce":
                            userAuthenticationModel.Nonce = claim.Value;
                            break;

                        // Altinn Specific claims
                        case AltinnCoreClaimTypes.UserId:
                            userAuthenticationModel.UserID = Convert.ToInt32(claim.Value);
                            break;

                        case AltinnCoreClaimTypes.PartyID:
                            userAuthenticationModel.PartyID = Convert.ToInt32(claim.Value);
                            break;

                        case AltinnCoreClaimTypes.AuthenticateMethod:
                            userAuthenticationModel.AuthenticationMethod = (AuthenticationMethod)System.Enum.Parse(typeof(AuthenticationMethod), claim.Value);
                            break;

                        case AltinnCoreClaimTypes.AuthenticationLevel:
                            userAuthenticationModel.AuthenticationLevel = (SecurityLevel)System.Enum.Parse(typeof(SecurityLevel), claim.Value);
                            break;

                        // ID-porten specific claims
                        case "pid":
                            userAuthenticationModel.SSN = claim.Value;
                            break;

                        case "amr":
                            userAuthenticationModel.AuthenticationMethod = GetAuthenticationMethod(claim.Value);
                            break;

                        case "acr":
                            userAuthenticationModel.AuthenticationLevel = GetAuthenticationLevel(claim.Value);
                            break;

                        default:
                            // Check for external identity claim
                            if (!string.IsNullOrEmpty(provider?.ExternalIdentityClaim) && claim.Type.Equals(provider?.ExternalIdentityClaim))
                            {
                                userAuthenticationModel.ExternalIdentity = claim.Value;
                            }

                            // General claims handling
                            if (provider?.ProviderClaims != null && provider.ProviderClaims.Contains(claim.Type))
                            {
                                userAuthenticationModel.ProviderClaims.TryAdd(claim.Type, new List<string>());
                                userAuthenticationModel.ProviderClaims[claim.Type].Add(claim.Value);
                            }

                            break;
                    }
                }

                if (userAuthenticationModel.AuthenticationMethod == AuthenticationMethod.NotDefined)
                {
                    userAuthenticationModel.AuthenticationMethod = (AuthenticationMethod)System.Enum.Parse(typeof(AuthenticationMethod), provider?.DefaultAuthenticationMethod);
                }
            }

            return userAuthenticationModel;
        }

        /// <summary>
        /// Get user information from the serializwd token string
        /// </summary>
        /// <param name="jwtToken">serialized jwt token string</param>
        /// <param name="provider">token provider</param>
        /// <returns></returns>
        public static UserAuthenticationModel GetUserFromToken(string jwtToken, OidcProvider provider)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            if (!string.IsNullOrEmpty(jwtToken))
            {
                JwtSecurityToken token = tokenHandler.ReadJwtToken(jwtToken);
                return GetUserFromToken(token, provider);
            }

            return null;
        }

        /// <summary>
        /// Converts IDporten acr claim �Authentication Context Class Reference� - The security level of assurance for the
        /// authentication. Possible values are Level3 (i.e. MinID was used) or Level4 (other eIDs).
        /// The level must be validated by the client.
        /// </summary>
        private static SecurityLevel GetAuthenticationLevel(string acr)
        {
            switch (acr)
            {
                case "Level3":
                    return Enum.SecurityLevel.Sensitive;
                case "Level4":
                    return Enum.SecurityLevel.VerySensitive;
            }

            return SecurityLevel.SelfIdentifed;
        }

        /// <summary>
        /// Converts external methods to internal  Minid-PIN, Minid-OTC, Commfides, Buypass, BankID, BankID Mobil or eIDAS
        /// </summary>
        private static AuthenticationMethod GetAuthenticationMethod(string amr)
        {
            switch (amr)
            {
                case "Minid-PIN":
                    return Enum.AuthenticationMethod.MinIDPin;
                case "Minid-OTC":
                    return Enum.AuthenticationMethod.MinIDOTC;
                case "Commfides":
                    return Enum.AuthenticationMethod.Commfides;
                case "Buypass":
                    return Enum.AuthenticationMethod.BuyPass;
                case "BankID":
                    return Enum.AuthenticationMethod.BankID;
                case "BankID Mobil":
                    return Enum.AuthenticationMethod.BankIDMobil;
                case "eIDAS":
                    return Enum.AuthenticationMethod.EIDAS;
            }

            return Enum.AuthenticationMethod.NotDefined;
        }
    }
}
