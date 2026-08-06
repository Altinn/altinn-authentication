#nullable enable
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Interim <see cref="ISelfIdentifiedLinkTokenCertificateProvider"/> that borrows the existing
    /// OIDC signing certificate(s) (issue #2035).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This keeps the link-token feature unblocked while a <b>dedicated</b> certificate is provisioned
    /// (KeyVault/config) as a follow-up. Until then, signature-level isolation from authentication
    /// tokens is not yet in place; the link token is still kept distinct via its dedicated issuer,
    /// audience and purpose claim, so it is rejected by every authentication endpoint and vice versa.
    /// </para>
    /// <para>
    /// Swapping to the dedicated certificate is a change to this class only - the token service depends
    /// solely on <see cref="ISelfIdentifiedLinkTokenCertificateProvider"/>.
    /// </para>
    /// </remarks>
    public class SelfIdentifiedLinkTokenCertificateProvider : ISelfIdentifiedLinkTokenCertificateProvider
    {
        private readonly IJwtSigningCertificateProvider _jwtSigningCertificateProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfIdentifiedLinkTokenCertificateProvider"/> class.
        /// </summary>
        public SelfIdentifiedLinkTokenCertificateProvider(IJwtSigningCertificateProvider jwtSigningCertificateProvider)
        {
            _jwtSigningCertificateProvider = jwtSigningCertificateProvider;
        }

        /// <inheritdoc/>
        public Task<List<X509Certificate2>> GetCertificates() => _jwtSigningCertificateProvider.GetCertificates();
    }
}
