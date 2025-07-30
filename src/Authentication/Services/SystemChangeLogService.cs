using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Service class for handling system change logs.
    /// </summary>
    public class SystemChangeLogService : ISystemChangeLogService
    {
        private readonly ISystemChangeLogRepository _systemChangeLogRepository;

        /// <summary>
        /// Constructor for the SystemChangeLogService class.
        /// </summary>
        /// <param name="systemChangeLogRepository">handler for systemchangelog repository</param>
        public SystemChangeLogService(ISystemChangeLogRepository systemChangeLogRepository)
        {
            _systemChangeLogRepository = systemChangeLogRepository;
        }

        /// <inheritdoc/>
        public Task<IList<SystemChangeLog>> GetChangeLogAsync(Guid systemInternalId, CancellationToken cancellationToken = default)
        {
            return _systemChangeLogRepository.GetChangeLogAsync(systemInternalId, cancellationToken);
        }
    }
}
