using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface ITokenService
    {
        /// <summary>
        /// Exchanges an authorization code for tokens.
        /// </summary>
        Task<TokenResult> ExchangeAuthorizationCodeAsync(TokenRequest exchangeRequest, CancellationToken ct);

        /// <summary>
        /// Refreshes tokens using a refresh token.
        /// </summary>
        Task<TokenResult> RefreshAsync(RefreshTokenRequest refreshRequest, CancellationToken ct);

        /// <summary>
        /// Create a token suitable for use in a cookie from an OIDC session.
        /// </summary>
        Task<string> CreateCookieToken(OidcSession oidcSession, CancellationToken ct);
    }
}
