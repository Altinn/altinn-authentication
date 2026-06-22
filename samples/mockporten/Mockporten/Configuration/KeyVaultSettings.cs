using System.ComponentModel.DataAnnotations;

namespace Mockporten.Configuration
{
    /// <summary>
    /// The key vault settings used to fetch certificate information from key vault
    /// </summary>
    public class KeyVaultSettings
    {
        /// <summary>
        /// Uri to keyvault
        /// </summary>
        [Required]
        public string KeyVaultURI { get; set; }

        /// <summary>
        /// Name of the certificate secret
        /// </summary>
        public string MaskinPortenCertSecretId { get; set; } = "idprovider-signing-cert-1";

    }

}
