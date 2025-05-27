using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Services.Interfaces;

#nullable enable

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The Service for the System Register
    /// </summary>
    public class SystemRegisterService : ISystemRegisterService
    {
        private readonly ISystemRegisterRepository _systemRegisterRepository;
        private readonly IResourceRegistryClient _resourceRegistryClient;
        private readonly IAccessManagementClient _accessManagementClient;

        /// <summary>
        /// The constructor
        /// </summary>
        public SystemRegisterService(
            ISystemRegisterRepository systemRegisterRepository,
            IResourceRegistryClient resourceRegistryClient,
            IAccessManagementClient accessManagementClient)
        {
            _systemRegisterRepository = systemRegisterRepository;
            _resourceRegistryClient = resourceRegistryClient;
            _accessManagementClient = accessManagementClient;
        }

        /// <inheritdoc/>
        public Task<List<RegisteredSystemResponse>> GetListRegSys(CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAllActiveSystems();
        }

        /// <inheritdoc/>
        public Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRightsForRegisteredSystem(systemId);
        }

        /// <inheritdoc/>
        public Task<List<AccessPackage>> GetAccessPackagesForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAccessPackagesForRegisteredSystem(systemId);
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
        public Task<RegisteredSystemResponse?> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRegisteredSystemById(systemId);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId)
        {
            return _systemRegisterRepository.UpdateRightsForRegisteredSystem(rights, systemId);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateAccessPackagesForRegisteredSystem(List<AccessPackage> accessPackages, string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.UpdateAccessPackagesForRegisteredSystem(accessPackages, systemId);
        }

        /// <inheritdoc/>
        public Task<bool> SetDeleteRegisteredSystemById(string id, Guid systemInternalId)
        {
            return _systemRegisterRepository.SetDeleteRegisteredSystemById(id, systemInternalId);
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
        public async Task<bool> DoesResourceIdExists(List<Right> rights, CancellationToken cancellationToken)
        {
            ServiceResource? resource = null;
            foreach (Right right in rights)
            {
                foreach (AttributePair resourceId in right.Resource)
                {
                    resource = await _resourceRegistryClient.GetResource(resourceId.Value);
                    if (resource == null)
                    {
                        return false;
                    }
                }                
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DoesAccessPackageExistsAndDelegable(List<AccessPackage> accessPackages, CancellationToken cancellationToken)
        {
            Package? package = null;
            foreach (AccessPackage accessPackage in accessPackages)
            {
                // get the urn value from the access package f.eks get regnskapsforer-med-signeringsrettighet from urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet
                string urnValue = accessPackage.Urn.Split(":")[3];
                package = await _accessManagementClient.GetAccessPackage(urnValue);
                if (package == null || !package.IsDelegable)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public Task DeleteMaskinportenClient(string clientInfoClientId, Guid currentSystemInternalId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.DeleteMaskinportenClient(clientInfoClientId, currentSystemInternalId, cancellationToken);
        }
    }
}
