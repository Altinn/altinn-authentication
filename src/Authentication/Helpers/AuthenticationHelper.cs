using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

#nullable enable

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper class for authentication process
    /// </summary>
    public static class AuthenticationHelper
    {
        private const string IdPortenAcrHigh = "idporten-loa-high";

        /// <summary>
        /// Get user information from the token
        /// </summary>
        /// <param name="jwtSecurityToken">jwt token</param>
        /// <param name="provider">authentication provider</param>
        /// <param name="accessToken">the access token</param>
        /// <returns>user information</returns>
        public static UserAuthenticationModel GetUserFromToken(JwtSecurityToken jwtSecurityToken, OidcProvider provider, JwtSecurityToken? accessToken = null)
        {
            UserAuthenticationModel userAuthenticationModel = new UserAuthenticationModel()
            {
                IsAuthenticated = true,
                ProviderClaims = new Dictionary<string, List<string>>(),
                Iss = provider.IssuerKey,
                AuthenticationMethod = AuthenticationMethod.NotDefined,
                TokenIssuer = jwtSecurityToken.Issuer,
                TokenSubject = jwtSecurityToken.Subject
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
                    userAuthenticationModel.AuthenticationMethod = System.Enum.Parse<AuthenticationMethod>(claim.Value);
                    continue;
                }

                if (claim.Type.Equals(AltinnCoreClaimTypes.AuthenticationLevel))
                {
                    userAuthenticationModel.AuthenticationLevel = System.Enum.Parse<SecurityLevel>(claim.Value);
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
                    userAuthenticationModel.Amr = [claim.Value];
                    userAuthenticationModel.AuthenticationMethod = GetAuthenticationMethod(claim.Value);
                    continue;
                }

                if (claim.Type.Equals("acr"))
                {
                    userAuthenticationModel.Acr = claim.Value;
                    userAuthenticationModel.AuthenticationLevel = GetAuthenticationLevelForIdPorten(claim.Value);
                    continue;
                }

                if (claim.Type.Equals("jti"))
                {
                    userAuthenticationModel.ExternalSessionId = claim.Value;
                    continue;
                }

                if (claim.Type.Equals("auth_time"))
                {
                    userAuthenticationModel.AuthTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(claim.Value));
                    continue;
                }

                if (claim.Type.Equals("sid"))
                {
                    userAuthenticationModel.Sid = claim.Value;
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
                userAuthenticationModel.AuthenticationMethod = System.Enum.Parse<AuthenticationMethod>(provider.DefaultAuthenticationMethod);
            }

            if (accessToken != null)
            {
                foreach (Claim claim in accessToken.Claims)
                {
                    // Scopes are only returned as part of the access token
                    if (claim.Type.Equals("scope"))
                    {
                        userAuthenticationModel.Scope = claim.Value;
                        continue;
                    }
                }
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
            switch (acr.ToLower(CultureInfo.InvariantCulture))
            {
                case "level0":
                case "idporten-loa-low":
                    return SecurityLevel.SelfIdentifed;
                case "level3":
                case "idporten-loa-substantial":
                    return SecurityLevel.Sensitive;
                case "level4":
                case "idporten-loa-high":
                    return SecurityLevel.VerySensitive;
                default:
                    return SecurityLevel.SelfIdentifed;
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
                case "SelfIdentified":
                    return AuthenticationMethod.SelfIdentified;
                case "Minid-APP":
                    return AuthenticationMethod.MinIDApp;
            }

            return Enum.AuthenticationMethod.NotDefined;
        }

        /// <summary>
        /// Maps the specified <see cref="AuthenticationMethod"/> to its corresponding Authentication Method Reference
        /// (AMR) value.
        /// </summary>
        /// <param name="method">The authentication method to map.</param>
        /// <returns>A string representing the AMR value for the specified authentication method.  Returns an empty string if the
        /// authentication method is not recognized.</returns>
        public static string GetAmrFromAuthenticationMethod(AuthenticationMethod method)
        {
            return method switch
            {
                AuthenticationMethod.MinIDPin => "Minid-PIN",
                AuthenticationMethod.MinIDOTC => "Minid-OTC",
                AuthenticationMethod.Commfides => "Commfides",
                AuthenticationMethod.BuyPass => "Buypass",
                AuthenticationMethod.BankID => "BankID",
                AuthenticationMethod.BankIDMobil => "BankID Mobil",
                AuthenticationMethod.EIDAS => "eIDAS",
                AuthenticationMethod.MaskinPorten => "maskinporten",
                AuthenticationMethod.IdportenTestId => "testid",
                AuthenticationMethod.AltinnPIN => "AltinnPIN",
                AuthenticationMethod.SelfIdentified => "SelfIdentified",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Inverse of GetAuthenticationLevelForIdPorten.
        /// Maps a SecurityLevel to a canonical ID-porten acr value.
        /// </summary>
        /// <param name="level">The Altinn security level.</param>
        /// <returns>Canonical acr string representing the given level.</returns>
        public static string GetAcrForAuthenticationLevel(SecurityLevel level)
        {
            return level switch
            {
                SecurityLevel.SelfIdentifed => "idporten-loa-low",
                SecurityLevel.Sensitive => "idporten-loa-substantial",
                SecurityLevel.VerySensitive => "idporten-loa-high",
                _ => "idporten-loa-low" // Fallback for levels without direct ID-porten mapping
            };
        }

        /// <summary>
        /// Checks if the identifier of the org number is valid
        /// </summary>
        /// <param name="id">the org number information of the vendor</param>
        /// <returns>true if the org number identifier is valid</returns>
        public static bool IsValidOrgIdentifier(string id)
        {
            string[] identityParts = id.Split(':');
            if (identityParts[0] != "0192")
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if authenticated user has write access
        /// </summary>
        /// <param name="systemOwner">the organisation number that owns the system</param>
        /// <param name="user">the authenticated user claim</param>
        /// <returns></returns>
        public static bool HasWriteAccess(string systemOwner, ClaimsPrincipal user)
        {
            if (!HasAdminAccess(user) && !IsOwnerOfSystem(systemOwner, user))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the authenticated user has admin scope
        /// </summary>
        /// <returns>true/false</returns>
        public static bool HasAdminAccess(ClaimsPrincipal user)
        {
            List<string> requiredScopes = new List<string>();
            requiredScopes.Add(AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN);
            if (ContainsRequiredScope(requiredScopes, user))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if system owner organisation matches organisation in claim
        /// </summary>
        /// <param name="systemOwner">the organisation number that owns the system</param>
        /// <param name="organisation">the authenticated organisation claim</param>
        /// <returns></returns>
        public static bool IsOwnerOfSystem(string systemOwner, ClaimsPrincipal organisation)
        {
            Console.WriteLine($"AuthorizationUtil // IsOwnerOfSystem // Checking organisation number in claims.");

            string? orgClaim = organisation?.Claims.Where(c => c.Type.Equals("consumer")).Select(c => c.Value).FirstOrDefault();

            if (orgClaim is null)
            {
                return false;
            }

            string orgNumber = GetOrganizationNumberFromClaim(orgClaim);

            Console.WriteLine($"AuthorizationUtil // IsOwnerOfSystem // Org claim: {orgClaim}.");

            if (systemOwner.Equals(orgNumber, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verifies a scope claim based on claimsprincipal.
        /// </summary>
        /// <param name="requiredScope">Requiered scope.</param>
        /// <param name="user">Claim principal from http context.</param>
        /// <returns>true if the given ClaimsPrincipal or on of its identities have contains the given scope.</returns>
        public static bool ContainsRequiredScope(List<string> requiredScope, ClaimsPrincipal user)
        {
            string? contextScope = user.Identities?
               .FirstOrDefault(i => i.AuthenticationType != null && i.AuthenticationType.Equals("AuthenticationTypes.Federation"))
               ?.Claims
               .Where(c => c.Type.Equals("urn:altinn:scope"))
               ?.Select(c => c.Value).FirstOrDefault();

            contextScope ??= user.Claims.Where(c => c.Type.Equals("scope")).Select(c => c.Value).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(contextScope))
            {
                return requiredScope.Any(scope => contextScope.Contains(scope, StringComparison.InvariantCultureIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Gets the organization number from the dictionary
        /// </summary>
        /// <param name="vendorId">the vendor information</param>
        /// <returns>the organization number</returns>
        /// <exception cref="ArgumentException">invalid organization identifier</exception>
        public static string? GetOrgNumber(string vendorId)
        {
            if (!string.IsNullOrEmpty(vendorId))
            {
                string[] identityParts = vendorId.Split(':');
                if (identityParts.Length > 0 && identityParts[0] != "0192")
                {
                    throw new ArgumentException("Invalid authority for the org number, unexpected ISO6523 identifier");
                }

                return identityParts[1];
            }

            return null;
        }

        /// <summary>
        /// Checks if the system id starts with the orgnumber of the owner
        /// </summary>
        /// <returns>true if the systemid starts with the orgnumber of the owner of the system</returns>
        public static bool DoesSystemIdStartWithOrgnumber(string vendorId, string systemId)
        {
            string? vendorOrgNumber = GetOrgNumber(vendorId);
            string orgnumberInSystemId = systemId.Split("_")[0];
            bool doesSystemStartWithOrg = orgnumberInSystemId == vendorOrgNumber;
            return doesSystemStartWithOrg;
        }

        /// <summary>
        /// Checks if the redirecturl matches the expected url pattern
        /// </summary>
        /// <param name="redirectUrls">the redirect url for a system</param>
        /// <returns>true if the url matches the expression</returns>
        public static bool IsValidRedirectUrl(List<Uri> redirectUrls)
        {
            foreach (Uri redirectUri in redirectUrls)
            {
                if (!IsValidAbsoluteUriWithHttps(redirectUri))
                {
                    return false;
                }
            }

            return true;

            static bool IsValidAbsoluteUriWithHttps(Uri uri)
            {
                return uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps;
            }
        }

        /// <summary>
        /// Map RegisteredSystem to bff object RegisteredSystemDTO
        /// </summary>
        /// <param name="registeredSystem">the RegisteredSystem object to convert to RegisteredSystemDTO</param>
        /// <returns>RegisteredSystemDTO of RegisteredSystem</returns>
        public static RegisteredSystemDTO MapRegisteredSystemToRegisteredSystemDTO(RegisteredSystemResponse registeredSystem)
        {
            return new RegisteredSystemDTO
            {
                Description = registeredSystem.Description,
                Name = registeredSystem.Name,
                Rights = registeredSystem.Rights,
                SystemId = registeredSystem.Id,
                SystemVendorOrgName = registeredSystem.SystemVendorOrgName,
                SystemVendorOrgNumber = GetOrgNumber(registeredSystem.SystemVendorOrgNumber),
                IsVisible = registeredSystem.IsVisible,
                AccessPackages = registeredSystem.AccessPackages
            };
        }

        /// <summary>
        /// Gets the organization number from the consumer claim
        /// </summary>
        /// <param name="claim">the user claim</param>
        /// <returns>organization number</returns>
        public static string GetOrganizationNumberFromClaim(string claim)
        {
            ConsumerClaim? consumerClaim;
            try
            {
                consumerClaim = JsonConvert.DeserializeObject<ConsumerClaim>(claim);
            }
            catch (JsonReaderException)
            {
                throw new ArgumentException("Invalid consumer claim: invalid JSON");
            }

            if (consumerClaim is null)
            {
                throw new ArgumentException("Invalid consumer claim: null");
            }

            if (consumerClaim.Authority != "iso6523-actorid-upis")
            {
                throw new ArgumentException("Invalid consumer claim: unexpected authority");
            }

            string[] identityParts = consumerClaim.Id.Split(':');
            if (identityParts[0] != "0192")
            {
                throw new ArgumentException("Invalid consumer claim: unexpected ISO6523 identifier");
            }

            return identityParts[1];
        }

        /// <summary>
        /// Gets the users id
        /// </summary>
        /// <param name="context">the http context</param>
        /// <returns>the logged in users id</returns>
        public static int GetUserId(HttpContext context)
        {
            var claim = context.User?.Claims.FirstOrDefault(c => c.Type.Equals(AltinnCoreClaimTypes.UserId));
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }

            return 0;
        }

        /// <summary>
        /// Gets the users id
        /// </summary>
        /// <param name="context">the http context</param>
        /// <returns>the logged in users id</returns>
        public static Guid GetPartyUuId(HttpContext context)
        {
            var claim = context.User?.Claims.FirstOrDefault(c => c.Type.Equals(AltinnCoreClaimTypes.PartyUUID));
            if (claim != null && Guid.TryParse(claim.Value, out Guid partyUuId))
            {
                return partyUuId;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Gets the users id
        /// </summary>
        /// <param name="context">the http context</param>
        /// <returns>the logged in users id</returns>
        public static int GetPartyId(HttpContext context)
        {
            var claim = context.User?.Claims.FirstOrDefault(c => c.Type.Equals(AltinnCoreClaimTypes.PartyID));
            if (claim != null && int.TryParse(claim.Value, out int partyId))
            {
                return partyId;
            }

            return 0;
        }

        /// <summary>
        /// Validate the resource id attribute "id"
        /// </summary>
        /// <param name="rights">the resources that the system gives rights to</param>
        /// <returns>true if resource id is valid</returns>
        public static bool IsResourceIdFormatValid(List<Right> rights)
        {
            var uniqueRights = new HashSet<string>();
            string pattern = @"^urn:altinn:resource$";

            if (rights != null)
            {
                foreach (var right in rights)
                {
                    foreach (AttributePair resourceId in right.Resource)
                    {
                        if (!Regex.IsMatch(resourceId.Id, pattern))
                        {
                            return false; // Invalid format
                        }
                    }
                }
            }

            return true; // All resourceids are in valid format
        }

        /// <summary>
        /// Check for duplicates in the rights
        /// </summary>
        /// <param name="rights">the resources that the system gives rights to</param>
        /// <returns>true if duplicate rights found</returns>
        public static bool HasDuplicateRights(List<Right> rights)
        {
            var uniqueRights = new HashSet<string>();

            if (rights != null)
            {
                foreach (var right in rights)
                {
                    var rightKey = $"{right.Action}:{string.Join(",", right.Resource.Select(r => $"{r.Id}:{r.Value}"))}";

                    if (!uniqueRights.Add(rightKey))
                    {
                        return true; // Duplicate found
                    }
                }
            }

            return false; // No duplicates
        }

        /// <summary>
        /// Checks if the AccessPackages list has duplicate values
        /// </summary>
        /// <param name="accessPackages">The list of access packages to check</param>
        /// <returns>true if duplicate access packages found</returns>
        public static bool HasDuplicateAccessPackage(List<AccessPackage> accessPackages)
        {
            var uniqueAccessPackages = new HashSet<string>();

            if (accessPackages != null)
            {
                foreach (var accessPackage in accessPackages)
                {
                    var accessPackageKey = $"{accessPackage.Urn}";

                    if (!uniqueAccessPackages.Add(accessPackageKey))
                    {
                        return true; // Duplicate found
                    }
                }
            }

            return false; // No duplicates
        }

        /// <summary>
        /// Validate that the RedirectUrl chosen is the same as one of the RedirectUrl's listed for the Registered System
        /// </summary>
        /// <param name="redirectURL">the RedirectUrl chosen</param>
        /// <param name="allowedRedirectUrls">the list of allowed redirect urls</param>
        /// <returns>Result or Problem</returns>
        public static Result<bool> ValidateRedirectUrl(string redirectURL, List<Uri> allowedRedirectUrls)
        {
            if (allowedRedirectUrls == null || allowedRedirectUrls.Count == 0)
            {
                return Problem.NoRedirectUrisFoundOnSystem;
            }

            List<Uri> redirectUrlsInSystem = allowedRedirectUrls;
            Uri chosenUri = new(redirectURL);

            foreach (var uri in redirectUrlsInSystem)
            {
                if (uri.GetLeftPart(UriPartial.Path) == chosenUri.GetLeftPart(UriPartial.Path))
                {
                    return true;
                }
            }

            return Problem.RedirectUriNotFound;
        }

        /// <summary>
        /// check if the system id contains space
        /// </summary>
        /// <param name="systemId">the id of the system</param>
        /// <returns>true id the systemid contains space</returns>
        public static bool HasSpaceInId(string systemId)
        {
            return systemId.Contains(' ');
        }

        /// <summary>
        /// Converts a list of <see cref="AccessPackage"/> objects into a list of their URNs.
        /// </summary>
        /// <param name="accessPackages">The list of <see cref="AccessPackage"/> objects to process. Cannot be null.</param>
        /// <returns>A list of strings containing the URNs of the provided <see cref="AccessPackage"/> objects.  Returns an empty
        /// list if <paramref name="accessPackages"/> is empty.</returns>
        public static List<string> GetPackagesArrayFromAccessPackages(List<AccessPackage> accessPackages)
        {
            List<string> packages = new List<string>();
            if (accessPackages == null || accessPackages.Count == 0)
            {
                return packages;
            }

            foreach (AccessPackage accessPackage in accessPackages)
            {
                packages.Add(accessPackage.Urn!.Split(':')[3]);
            }

            return packages;
        }

        /// <summary>
        /// Verifies if an ACR upgrade is needed based on the current and requested ACR values.
        /// </summary>
        internal static bool NeedAcrUpgrade(string? currentAcr, string[] reqeuestedAcr)
        {
           string requestAcr = string.Join(string.Empty, reqeuestedAcr);
           if (string.IsNullOrEmpty(currentAcr))
           {
               return false;
           }

           if (requestAcr.Equals(IdPortenAcrHigh) && (string.IsNullOrEmpty(currentAcr) || !currentAcr.Equals(IdPortenAcrHigh)))
           {
                return true;
           }

           return false;
        }
    }
}
