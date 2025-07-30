using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Npgsql;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Repository Interface for logging changes to system data.
    /// </summary>
    public interface ISystemChangeLogRepository
    {
        /// <summary>
        /// Logs a change to the system data.
        /// </summary>
        /// <param name="systemChangeLog">SystemChangeLog</param>
        /// <param name="conn">Connection to use for the operation</param>
        /// <param name="transaction">Transaction to use for the operation.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task LogChangeAsync(SystemChangeLog systemChangeLog, NpgsqlConnection conn, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

        Task<IList<SystemChangeLog>> GetChangeLogAsync(Guid systemInternalId, CancellationToken cancellationToken = default);
    }
}
