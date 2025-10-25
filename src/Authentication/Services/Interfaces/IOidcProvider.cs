#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Interface for communicating
    /// </summary>
    public interface IOidcProvider
    {
        /// <summary>
        /// Gets tokens from the OIDC provider. Response shape varies by scopes/client.
        /// Contract: returns a non-null response on success; throws on transport/protocol/parse errors.
        /// </summary>
        Task<OidcCodeResponse> GetTokens(string authorizationCode, OidcProvider provider, string redirect_uri, string? codeVerifier, CancellationToken cancellationToken = default);
    }
}
