using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IUnregisteredClientRepository
    {
        Task InsertAsync(UnregisteredClientRequestCreate create, CancellationToken ct);

        Task<UnregisteredClientRequest?> GetByRequestIdAsync(Guid requestId, CancellationToken ct);

        /// <summary>
        /// Generic status transition by request id (e.g., cancel/error). Will also set completed_at if terminal.
        /// Returns false on not found or already terminal (unless setting same status).
        /// </summary>
        Task<bool> MarkStatusAsync(Guid requestId, UnregisteredClientRequestStatus newStatus, DateTimeOffset whenUtc, string? handledByCallback, CancellationToken ct);

        /// <summary>
        /// Hard-delete expired rows. Use a limit to avoid long transactions.
        /// Returns number of rows deleted.
        /// </summary>
        Task<int> SweepExpiredAsync(DateTimeOffset nowUtc, int limit, CancellationToken ct);
    }
}
