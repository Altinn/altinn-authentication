using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Defines methods for managing refresh tokens.
    /// </summary>
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Gets or creates a family ID for the given client ID, subject ID, and OP SID.
        /// </summary>
        Task<Guid> GetOrCreateFamilyAsync(string clientId, string subjectId, string opSid, CancellationToken cancellationToken);

        /// <summary>
        /// Inserts a new refresh token record.
        /// </summary>
        Task InsertAsync(RefreshTokenRow row, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a refresh token by its lookup key.
        /// </summary>
        Task<RefreshTokenRow?> GetByLookupKeyAsync(byte[] lookupKey, CancellationToken cancellationToken);

        /// <summary>
        /// Marks a refresh token as used, linking it to the new rotated token.
        /// </summary>
        Task MarkUsedAsync(Guid tokenId, Guid rotatedToTokenId, CancellationToken cancellationToken);

        /// <summary>
        /// Revokes a refresh token by its ID, providing a reason for revocation. 
        /// </summary>
        Task RevokeAsync(Guid tokenId, string reason, CancellationToken cancellationToken);

        /// <summary>
        /// Revokes all refresh tokens in a family by the family ID, providing a reason for revocation.
        /// </summary>
        Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all family IDs associated with the given OP SID.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetFamiliesByOpSidAsync(string opSid, CancellationToken cancellationToken);
    }
}
