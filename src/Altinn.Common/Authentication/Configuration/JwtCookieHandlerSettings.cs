using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Common.Authentication.Configuration
{
    /// <summary>
    /// Settings for the JwtCookie handling.
    /// </summary>
    public class JwtCookieHandlerSettings
    {
        /// <summary>
        /// Gets or sets the wellknown configuration endpoint for Maskinporten.
        /// </summary>
        public string MaskinportenWellKnownConfigEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the valid issuer for Maskinporten.
        /// </summary>
        public string MaskinportenValidIssuer { get; set; }
    }
}
