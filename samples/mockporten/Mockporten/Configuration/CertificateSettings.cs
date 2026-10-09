using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockporten.Configuration
{

    /// <summary>
    /// The certificate settings
    /// </summary>
    public class CertificateSettings
    {
        /// <summary>
        /// The name of the certificate
        /// </summary>
        public string CertificateName { get; set; }

        /// <summary>
        /// The password of the certificate
        /// </summary>
        public string CertificatePwd { get; set; }

        /// <summary>
        /// The path to the certificate
        /// </summary>
        public string CertificatePath { get; set; }
    }
}
