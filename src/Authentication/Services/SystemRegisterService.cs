using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The Service for the System Register
    /// </summary>
    public class SystemRegisterService : ISystemRegisterService
    {
        private readonly ISystemRegisterRepository _systemRegisterRepository;

        /// <summary>
        /// The constructor
        /// </summary>
        public SystemRegisterService(ISystemRegisterRepository systemRegisterRepository)
        {
            _systemRegisterRepository = systemRegisterRepository;
        }

        /// <summary>
        /// Inheritdoc
        /// </summary>
        public Task<List<RegisteredSystem>> GetListRegSys(CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAllActiveSystems();
        }

        /// <inheritdoc/>
        public Task<List<DefaultRights>> GetDefaultRightsForRegisteredSystem(Guid systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetDefaultRightsForRegisteredSystem(systemId);
        }
    }
}
