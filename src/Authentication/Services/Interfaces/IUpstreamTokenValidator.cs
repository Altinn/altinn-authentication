#nullable enable
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Interface for validating upstream tokens.
    /// </summary>
    public interface IUpstreamTokenValidator
    {
        /// <summary>
        /// Validates an upstream token and returns a JwtSecurityToken if valid.
        /// </summary>
        /// <param name="token">The raw token.</param>
        /// <param name="provider">The provider that issued it.</param>
        /// <param name="kind">
        /// Which kind of token this is. Decides whether the id_token rules of
        /// <see cref="OidcProvider.StrictIdTokenValidation"/> apply. Stated by the caller rather than
        /// inferred, because an id_token presented as <c>id_token_hint</c> carries no nonce.
        /// </param>
        /// <param name="nonce">The nonce to require, or <c>null</c> when none applies.</param>
        /// <param name="cancellationToken">Cancellation.</param>
        Task<JwtSecurityToken> ValidateTokenAsync(string token, OidcProvider provider, UpstreamTokenKind kind, string? nonce, CancellationToken cancellationToken = default);
    }
}
