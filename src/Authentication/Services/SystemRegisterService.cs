using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
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

        /// <inheritdoc/>
        public Task<List<RegisterSystemResponse>> GetListRegSys(CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAllActiveSystems();
        }

        /// <inheritdoc/>
        public Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRightsForRegisteredSystem(systemId);
        }

        /// <inheritdoc/>
        public Task<bool> CreateClient(string clientId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.CreateClient(clientId);
        }

        /// <inheritdoc/>
        public Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, CancellationToken cancellation = default)
        {
            int count = system.Rights.Count;
            string[] defaultRights = new string[count];
            int i = 0;
            if (system.Rights != null)
            {
                foreach (Right defaultRight in system.Rights)
                {
                    defaultRights[i] = ConvertDefaultRightsToString(defaultRight.Resource);
                    i++;
                }
            }

            return _systemRegisterRepository.CreateRegisteredSystem(system, defaultRights);
        }

        private static string ConvertDefaultRightsToString(List<AttributePair> pairList)
        {
            string str = string.Empty;

            foreach (AttributePair pair in pairList)
            {
                str += "{" + pair.Id + "=" + pair.Value + "}";
            }

            return str;
        }

        /// <summary>
        /// Gets the registered system's information
        /// </summary>
        /// <param name="systemId">the system id</param>
        /// <param name="cancellation">the cancellation token</param>
        /// <returns></returns>
        public Task<RegisterSystemResponse> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRegisteredSystemById(systemId);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId)
        {
            return _systemRegisterRepository.UpdateRightsForRegisteredSystem(rights, systemId);
        }

        /// <inheritdoc/>
        public Task<bool> SetDeleteRegisteredSystemById(string id)
        {
            return _systemRegisterRepository.SetDeleteRegisteredSystemById(id);
        }
    }
}
