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
        Task<JwtSecurityToken> ValidateTokenAsync(string token, OidcProvider provider, string? nonce, CancellationToken cancellationToken = default);
    }
}
