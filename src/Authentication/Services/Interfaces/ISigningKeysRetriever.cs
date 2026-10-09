using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Reads what an OIDC provider publishes in its discovery document.
    /// </summary>
    public interface ISigningKeysRetriever
    {
        /// <summary>
        /// Get the signing keys published by the given endpoint.
        /// </summary>
        /// <param name="url">The full address of the published configuration.</param>
        /// <returns>The published signing keys.</returns>
        Task<ICollection<SecurityKey>> GetSigningKeys(string url);

        /// <summary>
        /// Get the issuer identifier the provider asserts in its discovery document.
        /// </summary>
        /// <remarks>
        /// Behind this interface rather than read from the configuration helper directly, so the
        /// callback issuer check can run in tests without a network call — the same reason the
        /// signing keys already are.
        /// </remarks>
        /// <param name="url">The full address of the published configuration.</param>
        /// <param name="cancellationToken">Observed while waiting for the document.</param>
        /// <returns>The <c>issuer</c> value, or <c>null</c> when the document states none.</returns>
        Task<string?> GetIssuer(string url, CancellationToken cancellationToken = default);
    }
}
