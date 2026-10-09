#nullable enable
using System.Collections.Generic;
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

        /// <summary>
        /// Pushes the authorization request parameters to the provider's PAR endpoint (RFC 9126)
        /// and returns the reference to use at the authorize endpoint.
        /// <para>
        /// Same contract as <see cref="GetTokens"/>: <c>null</c> means the provider refused, was
        /// unreachable, or answered with something unusable, with the cause already logged and
        /// counted. There is deliberately no fallback to a front-channel request — a provider that
        /// requires PAR would reject it anyway, and sending the parameters through the browser
        /// after failing to push them would defeat the point of pushing them.
        /// </para>
        /// </summary>
        Task<PushedAuthorizationResponse?> PushAuthorizationRequest(OidcProvider provider, IDictionary<string, string> parameters, CancellationToken cancellationToken = default);
    }
}
