using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Token service interface
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Generates an Altinn Token (JWT) for the provided claims principal.
        /// </summary>
        /// <param name="principal">Claims</param>
        /// <param name="expires">Token expiration</param>
        /// <returns></returns>
        Task<string> GenerateToken(ClaimsPrincipal principal, DateTime? expires = null);
    }
}
