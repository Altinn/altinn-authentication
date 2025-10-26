using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Defines methods for managing authorization codes.
    /// </summary>
    public interface IAuthorizationCodeRepository
    {
        /// <summary>
        /// Inserts a new authorization code record.
        /// </summary>
        Task InsertAsync(AuthorizationCodeCreate codeCreate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an authorization code record by code.
        /// </summary>
        Task<AuthCodeRow?> GetAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consumes (marks as used) an authorization code if it exists, matches the given client ID and redirect URI, and is unexpired.
        /// </summary>
        Task<bool> TryConsumeAsync(string code, string clientId, Uri redirectUri, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    }
}
