using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Common.Authentication.Models
{
    /// <summary>
    /// Settings for the oidc provider.
    /// </summary>
    public class OidcProvider
    {
        /// <summary>
        /// Gets or sets the wellknown configuration endpoint.
        /// </summary>
        public string WellKnownConfigEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the valid issuer.
        /// </summary>
        public string Issuer { get; set; }
    }
}
