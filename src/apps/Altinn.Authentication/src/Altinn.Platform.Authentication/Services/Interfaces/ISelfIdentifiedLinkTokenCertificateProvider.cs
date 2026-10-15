#nullable enable
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Provides the certificate(s) used to sign and validate the self-identified account-link token
    /// (issue #2035).
    /// </summary>
    /// <remarks>
    /// This is intentionally a <b>separate</b> abstraction from <see cref="IJwtSigningCertificateProvider"/>:
    /// the link token must never be signable or validatable with the OIDC/authentication signing keys.
    /// The interim implementation may borrow the existing provider's key material, but the dedicated
    /// certificate is wired through this seam as a follow-up without touching the token service.
    /// </remarks>
    public interface ISelfIdentifiedLinkTokenCertificateProvider
    {
        /// <summary>
        /// Gets the current and previous version(s) of the link-token certificate. The newest
        /// certificate with a private key is used for signing; all are accepted for validation.
        /// </summary>
        Task<List<X509Certificate2>> GetCertificates();
    }
}
