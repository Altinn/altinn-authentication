using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
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
        public Task<List<RegisteredSystem>> GetListRegSys(CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAllActiveSystems();
        }

        /// <inheritdoc/>
        public Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRightsForRegisteredSystem(systemId);
        }

        /// <inheritdoc/>
        public Task<bool> CreateClient(string clientId, Guid systemInteralId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.CreateClient(clientId, systemInteralId);
        }

        /// <inheritdoc/>
        public Task<bool> DoesClientIdExists(List<string> clientId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.DoesClientIdExists(clientId);
        }

        /// <inheritdoc/>
        public Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.CreateRegisteredSystem(system);
        }

        /// <summary>
        /// Gets the registered system's information
        /// </summary>
        /// <param name="systemId">the system id</param>
        /// <param name="cancellation">the cancellation token</param>
        /// <returns></returns>
        public Task<RegisteredSystem> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default)
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

        /// <inheritdoc/>
        public Task<bool> UpdateWholeRegisteredSystem(RegisterSystemRequest updateSystem, string systemId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.UpdateRegisteredSystem(updateSystem);
        }

        /// <inheritdoc/>
        public Task<List<MaskinPortenClientInfo>> GetMaskinportenClients(List<string> clientId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.GetMaskinportenClients(clientId);
        }

        /// <inheritdoc/>
        public async Task<Result<RegisteredSystemDTO>> GetRegisteredSystemDto(string systemId, CancellationToken cancellationToken)
        {
            var result = await _systemRegisterRepository.GetRegisteredSystemById(systemId);
            if (result is null)
            {
                return Problem.SystemIdNotFound;
            }

            return new RegisteredSystemDTO()
            {
                Description = result.Description,
                Name = result.Name,
                Rights = result.Rights,
                SystemId = result.SystemId,
                SystemVendorOrgNumber = result.SystemVendorOrgNumber
            };
        }
    }
}
