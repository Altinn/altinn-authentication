namespace Altinn.Authentication.Integration.Configuration
{
#nullable enable
    /// <summary>
    /// General configuration settings
    /// </summary>
    public class PlatformSettings
    {
        /// <summary>
        /// Open Id Connect Well known endpoint
        /// </summary>
        public string? OpenIdWellKnownEndpoint { get; set; }

        /// <summary>
        /// Name of the cookie for where JWT is stored
        /// </summary>
        public string? JwtCookieName { get; set; }

        /// <summary>
        /// Gets or sets the profile api endpoint.
        /// </summary>
        public string? ApiProfileEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the register api endpoint.
        /// </summary>
        public string? ApiRegisterEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the resource registry api endpoint.
        /// </summary>
        public string? ApiResourceRegistryEndpoint { get; set; }c

        /// <summary>
        /// Gets or sets the subscriptionkey.
        /// </summary>
        public string? SubscriptionKey { get; set; }

        /// <summary>
        /// Gets or sets the SubscriptionKeyHeaderName
        /// </summary>
        public string? SubscriptionKeyHeaderName { get; set; }
        
        /// <summary>
        /// Endpoint for authentication
        /// </summary>
        public string? ApiAuthenticationEndpoint { get; set; }

        /// <summary>
        /// Altinn Authorization base url
        /// </summary>
        public string? ApiAuthorizationEndpoint { get; set; }
    }
}
