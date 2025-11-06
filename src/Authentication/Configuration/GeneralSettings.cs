using System;

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// General configuration settings
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// Gets or sets the name of the SBL authentication cookie.
        /// </summary>
        public string SblAuthCookieEnvSpecificName { get; set; }

        /// <summary>
        /// Gets or sets the name of the SBL authentication cookie. Only used until SblAuthCookieEnvSpecificName
        /// is rolled out in all environments.
        public string SblAuthCookieName { get; set; }

        /// <summary>
        /// Gets or sets the name of the JSON Web Token cookie.
        /// </summary>
        public string JwtCookieName { get; set; }

        /// <summary>
        /// Gets or sets the AltinnParty cookie name
        /// </summary>
        public string AltinnPartyCookieName { get; set; }

        /// <summary>
        /// Name of the partyuuid cookie where selected party uuid is stored
        /// </summary>
        public string AltinnPartyUuidCookieName { get; set; } = "AltinnPartyUuid";

        /// <summary>
        /// Gets or sets the AltinnLogoutInfo cookie name
        /// </summary>
        public string AltinnLogoutInfoCookieName { get; set; }

        /// <summary>
        /// Gets or sets the bridge authentication api endpoint
        /// </summary>
        public string BridgeAuthnApiEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the bridge authentication api endpoint
        /// </summary>
        public string BridgeProfileApiEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the sbl redirect endpoint
        /// </summary>
        public string SBLRedirectEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the sbl logout endpoint
        /// </summary>
        public string SBLLogoutEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the platform endpoint
        /// </summary>
        public string PlatformEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the claims identity
        /// </summary>
        public string ClaimsIdentity { get; set; }

        /// <summary>
        /// Gets or sets the number of minutes the JSON Web Token and the cookie is valid.
        /// </summary>
        public int JwtValidityMinutes { get; set; }

        /// <summary>
        /// The number of minutes the ID token is valid
        /// </summary>
        public int OidcTokenValidityMinutes { get; set; } = 10;

        /// <summary>
        /// The number of minutes the refresh token is valid
        /// </summary>
        public int OidcRefreshTokenValidityMinutes { get; set; } = 30;

        /// <summary>
        /// Max session time in minutes
        /// </summary>
        public int MaxSessionTimeInMinutes { get; set; }

        /// <summary>
        /// Gets or sets the hostname
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Gets or sets the BaseUrl
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets URL of the well known configuration endpoint for Maskinporten.
        /// </summary>
        public string MaskinportenWellKnownConfigEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the alternative URL of the well known configuration endpoint for Maskinporten.
        /// </summary>
        public string MaskinportenWellKnownAlternativeConfigEndpoint { get; set; }

        /// <summary>
        /// Gets url of the well known configuration endpoint for ID-porten from environment variable.
        /// </summary>
        public string IdPortenWellKnownConfigEndpoint { get; set; }

        /// <summary>
        /// Gets url of the alternative well known configuration endpoint for ID-porten from environment variable.
        /// </summary>
        public string IdPortenAlternativeWellKnownConfigEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the url to the json file which holds the valid organisation entries (which inclides name, organisation number and org identifier)
        /// </summary>
        public string OrganisationRepositoryLocation { get; set; }

        /// <summary>
        /// Gets or sets the URL of the Altinn Open ID Connect well-known configuration endpoint.
        /// </summary>
        public string OpenIdWellKnownEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the number of hours to wait before a new certificate is being used to
        /// sign new JSON Web tokens.
        /// </summary>
        /// <remarks>
        /// The logic use the NotBefore property of a certificate. This means that uploading a
        /// certificate that has been valid for a few days might cause it to be used immediately.
        /// Take care not to upload "old" certificates.
        /// </remarks>
        public int JwtSigningCertificateRolloverDelayHours { get; set; }

        /// <summary>
        /// Server secret for hashing the OIDC refresh token
        /// </summary>
        public required string OidcRefreshTokenPepper { get; set; }

        /// <summary>
        /// Gets the Altinn Open ID Connect (OIDC) Issuer URL.
        /// </summary>
        public string AltinnOidcIssuerUrl
        {
            get
            {
                return $"{PlatformEndpoint.TrimEnd('/')}/authentication/api/v1/openid/";
            }
        }

        /// <summary>
        /// Gets the authorize endpoint for Altinn OIDC
        /// </summary>
        public string AltinnOidcAuthorizeEndpoint
        {
            get
            {
                return $"{PlatformEndpoint.TrimEnd('/')}/authentication/api/v1/authorize/";
            }
        }

        /// <summary>
        /// End session endpoint for Altinn OIDC
        /// </summary>
        public string AltinnOidcEndSessionEndpoint
        {
            get
            {
                return $"{PlatformEndpoint.TrimEnd('/')}/authentication/api/v1/openid/logout/";
            }
        }

        /// <summary>
        /// Gets the token endpoint for Altinn OIDC
        /// </summary>
        public string AltinnOidcTokenEndpoint
        {
            get
            {
                return $"{PlatformEndpoint.TrimEnd('/')}/authentication/api/v1/token/";
            }
        }

        /// <summary>
        /// Get or sets the value indicating if OIDC authentication is enabled
        /// </summary>
        public bool EnableOidc { get; set; }

        /// <summary>
        /// Get or sets the default oidc provider
        /// </summary>
        public string DefaultOidcProvider { get; set; }

        /// <summary>
        /// Defines if OIDC is the default authentication
        /// </summary>
        public bool ForceOidc { get; set; }

        /// <summary>
        /// Scopes set when there is no client id (Altinn Apps) or source is Altinn 2
        /// </summary>
        public string DefaultPortalScopes { get; set; } = "openid digdir:dialogporten.noconsent altinn:portal/enduser altinn:instances.read";

        /// <summary>
        /// Enables the authorization server
        /// </summary>
        public bool AuthorizationServerEnabled { get; set; }

        /// <summary>
        /// Name of Oidc Nonce cookie
        /// </summary>
        public string OidcNonceCookieName { get; set; } = "oidcnonce";

        /// <summary>
        /// Cookie to store original 
        /// </summary>
        public string AuthnGoToCookieName { get; set; } = "authngoto";

        /// <summary>
        /// Scopes for partner authentication with exchange. 
        /// </summary>
        public string PartnerScopes { get; set; }

        /// <summary>
        /// Defines the Altinn session cookie name
        /// </summary>
        public string AltinnSessionCookieName { get; set; } = "altinnsession";
    }
}
