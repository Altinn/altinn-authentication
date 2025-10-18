using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Interface for managing downstream login transactions.
    /// </summary>
    public interface ILoginTransactionRepository
    {
        /// <summary>
        /// Inserts a new downstream login transaction and returns the stored row
        /// (including generated <c>request_id</c> and timestamps).
        /// </summary>
        Task<LoginTransaction> InsertAsync(LoginTransactionCreate create, CancellationToken ct = default);

        /// <summary>
        /// Gets a downstream login transaction by its <c>request_id</c>.
        /// </summary>
        Task<LoginTransaction?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);
    }
}
