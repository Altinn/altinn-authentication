using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The Service for the System Register
    /// </summary>
    public class SystemRegisterService : ISystemRegisterService
    {
        private readonly ISystemRegisterRepository _systemRegisterRepository;
        private readonly IPartiesClient _partiesClient;

        /// <summary>
        /// The constructor
        /// </summary>
        public SystemRegisterService(
            ISystemRegisterRepository systemRegisterRepository,
            IPartiesClient partiesClient)
        {
            _systemRegisterRepository = systemRegisterRepository;
            _partiesClient = partiesClient;
        }

        /// <inheritdoc/>
        public async Task<List<SystemRegisterDTO>> GetListRegSys(CancellationToken cancellation = default)
        {
            List<SystemRegisterDTO> listDTO = [];

            var dbList = await _systemRegisterRepository.GetAllActiveSystems();
            foreach (var sys in dbList) 
            {
                listDTO.Add(
                    new()
                    {
                        Description = sys.Description,
                        Name = sys.Name,
                        Rights = sys.Rights,
                        SystemId = sys.SystemId,                        
                        SystemVendorOrgNumber = sys.SystemVendorOrgNumber
                    });
            }

            foreach (var dto in listDTO)
            {
                dto.SystemVendorOrgName = await EnrichSystemVendorOrgName(dto.SystemVendorOrgNumber);
            }

            return listDTO;
        }

        private async Task<string> EnrichSystemVendorOrgName(string orgno)
        {
            Register.Models.Organization org = await _partiesClient.GetOrganizationAsync(orgno);
            return org.Name;
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
        public Task<Guid?> CreateRegisteredSystem(SystemRegisterRequest system, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.CreateRegisteredSystem(system);
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

        /// <inheritdoc/>
        public Task<bool> UpdateWholeRegisteredSystem(SystemRegisterRequest updateSystem, string systemId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.UpdateRegisteredSystem(updateSystem);
        }

        /// <inheritdoc/>
        public Task<List<MaskinPortenClientInfo>> GetMaskinportenClients(List<string> clientId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.GetMaskinportenClients(clientId);
        }

        /// <inheritdoc/>
        public async Task<Result<SystemRegisterDTO>> GetRegisteredSystemDto(string systemId, CancellationToken cancellationToken)
        {            
            var model = await _systemRegisterRepository.GetRegisteredSystemById(systemId);
            if (model is null)
            {
                return Problem.SystemIdNotFound;
            }                

            return new SystemRegisterDTO
            {
                Description = model.Description,
                Name = model.Name,
                Rights = model.Rights,
                SystemId = model.SystemId,
                SystemVendorOrgName = model.SystemVendorOrgName,
                SystemVendorOrgNumber = model.SystemVendorOrgNumber,
            };
        }
    }
}
