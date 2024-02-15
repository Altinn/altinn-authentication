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
                Iss = provider.IssuerKey,
                AuthenticationMethod = AuthenticationMethod.NotDefined
            };

            foreach (Claim claim in jwtSecurityToken.Claims)
            {
                // General OIDC claims
                if (claim.Type.Equals("nonce"))
                {
                    userAuthenticationModel.Nonce = claim.Value;
                    continue;
                }

                // Altinn Specific claims
                if (claim.Type.Equals(AltinnCoreClaimTypes.UserId))
                {
                    userAuthenticationModel.UserID = Convert.ToInt32(claim.Value);
                    continue;
                }

                if (claim.Type.Equals(AltinnCoreClaimTypes.PartyID))
                {
                    userAuthenticationModel.PartyID = Convert.ToInt32(claim.Value);
                    continue;
                }

                if (claim.Type.Equals(AltinnCoreClaimTypes.AuthenticateMethod))
                {
                    userAuthenticationModel.AuthenticationMethod = (Enum.AuthenticationMethod)System.Enum.Parse(typeof(Enum.AuthenticationMethod), claim.Value);
                    continue;
                }

                if (claim.Type.Equals(AltinnCoreClaimTypes.AuthenticationLevel))
                {
                    userAuthenticationModel.AuthenticationLevel = (Enum.SecurityLevel)System.Enum.Parse(typeof(Enum.SecurityLevel), claim.Value);
                    continue;
                }

                // ID-porten specific claims
                if (claim.Type.Equals("pid"))
                {
                    userAuthenticationModel.SSN = claim.Value;
                    continue;
                }

                if (claim.Type.Equals("amr"))
                {
                    userAuthenticationModel.AuthenticationMethod = GetAuthenticationMethod(claim.Value);
                    continue;
                }

                if (claim.Type.Equals("acr"))
                {
                    userAuthenticationModel.AuthenticationLevel = GetAuthenticationLevelForIdPorten(claim.Value);
                    continue;
                }

                if (!string.IsNullOrEmpty(provider.ExternalIdentityClaim) && claim.Type.Equals(provider.ExternalIdentityClaim))
                {
                    userAuthenticationModel.ExternalIdentity = claim.Value;
                }

                // General claims handling
                if (provider.ProviderClaims != null && provider.ProviderClaims.Contains(claim.Type))
                {
                    if (!userAuthenticationModel.ProviderClaims.ContainsKey(claim.Type))
                    {
                        userAuthenticationModel.ProviderClaims.Add(claim.Type, new List<string>());
                    }

                    userAuthenticationModel.ProviderClaims[claim.Type].Add(claim.Value);
                }
            }

            if (userAuthenticationModel.AuthenticationMethod == AuthenticationMethod.NotDefined)
            {
                userAuthenticationModel.AuthenticationMethod = (AuthenticationMethod)System.Enum.Parse(typeof(AuthenticationMethod), provider.DefaultAuthenticationMethod);
            }

            return userAuthenticationModel;
        }
       
        /// <summary>
        /// Converts IDporten acr claim Authentication Context Class Reference - The security level of assurance for the
        /// authentication. Possible values are Level3 (i.e. MinID was used) or Level4 (other eIDs).
        /// The level must be validated by the client.
        /// </summary>
        public static SecurityLevel GetAuthenticationLevelForIdPorten(string acr)
        {
            switch (acr)
            {
                case "Level0":
                    return Enum.SecurityLevel.NotSensitive;
                case "Level3":
                    return Enum.SecurityLevel.Sensitive;
                case "Level4":
                    return Enum.SecurityLevel.VerySensitive;
                default: 
                    return Enum.SecurityLevel.NotSensitive;
            }
        }

        /// <summary>
        /// Converts external methods to internal  Minid-PIN, Minid-OTC, Commfides, Buypass, BankID, BankID Mobil or eIDAS
        /// </summary>
        public static AuthenticationMethod GetAuthenticationMethod(string amr)
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
                case "maskinporten":
                    return Enum.AuthenticationMethod.MaskinPorten;
                case "testid":
                    return AuthenticationMethod.IdportenTestId;
            }

            return Enum.AuthenticationMethod.NotDefined;
        }
    }
}
