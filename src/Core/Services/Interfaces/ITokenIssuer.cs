using Altinn.Platform.Authentication.Core.Models.Oidc;

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
        /// <param name="code">Authorization code record (subject, client binding, scopes…)</param>
        /// <param name="now">Current time (UTC)</param>
        /// <param name="ct">Cancellation</param>
        /// <returns>(jwt, expiresAt, scopeString)</returns>
        Task<(string accessToken, DateTimeOffset expiresAt, string scope)> CreateAccessTokenAsync(
            AuthCodeRow code, DateTimeOffset now, CancellationToken ct = default);

        /// <summary>
        /// Create a signed ID token for the given authorization code context and client.
        /// Returns null if <c>openid</c> was not requested.
        /// </summary>
        Task<string?> CreateIdTokenAsync(
            AuthCodeRow code, OidcClient client, DateTimeOffset now, CancellationToken ct = default);
    }
}
