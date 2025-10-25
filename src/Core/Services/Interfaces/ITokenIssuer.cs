using Altinn.Platform.Authentication.Core.Models.Oidc;
using System.Security.Claims;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    /// <summary>
    /// Mints OAuth 2.0 / OIDC tokens for Altinn Authentication (the OP).
    /// </summary>
    public interface ITokenIssuer
    {
        /// <summary>
        /// Create a signed access token for the given authorization code context.
        /// </summary>
        Task<string> CreateAccessTokenAsync(ClaimsPrincipal principal, DateTimeOffset tokenExpiration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a signed ID token for the given authorization code context and client.
        /// Returns null if <c>openid</c> was not requested.
        /// </summary>
        Task<string> CreateIdTokenAsync(ClaimsPrincipal principal, OidcClient client, DateTimeOffset tokenExpiration, CancellationToken cancellationToken = default);
    }
}
