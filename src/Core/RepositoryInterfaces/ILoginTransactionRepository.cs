using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface ILoginTransactionRepository
    {
        /// <summary>
        /// Inserts a new downstream login transaction and returns the stored row
        /// (including generated <c>request_id</c> and timestamps).
        /// </summary>
        Task<LoginTransaction> InsertAsync(LoginTransactionCreate create, CancellationToken ct = default);
    }
}
