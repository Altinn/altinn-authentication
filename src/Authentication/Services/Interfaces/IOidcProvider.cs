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
        /// Contract: returns a usable response on success, and <c>null</c> when the upstream refused
        /// the request, was unreachable, or answered with something that is not a token response.
        /// The implementation logs and counts the cause before returning <c>null</c>, so callers only
        /// need to decide what the user sees. Throws only if the caller's own token is cancelled.
        /// </summary>
        Task<OidcCodeResponse?> GetTokens(string authorizationCode, OidcProvider provider, string redirect_uri, string? codeVerifier, CancellationToken cancellationToken = default);
    }
}
