using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Interface for the System Change Log Service.
    /// </summary>
    public interface ISystemChangeLogService
    {
        /// <summary>
        /// Gets the change log for a specific system identified by its internal ID.
        /// </summary>
        /// <param name="systemInternalId">the internal id of the system</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        Task<IList<SystemChangeLog>> GetChangeLogAsync(Guid systemInternalId, CancellationToken cancellationToken = default);
    }
}
