using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
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
        private readonly ISystemChangeLogRepository _systemChangeLogRepository;
        private readonly IResourceRegistryClient _resourceRegistryClient;
        private readonly IAccessManagementClient _accessManagementClient;

        private static readonly HashSet<ResourceType> WhitelistedResourceTypes = new()
        {
            ResourceType.AltinnApp,
            ResourceType.Systemresource,
            ResourceType.Default,
            ResourceType.CorrespondenceService,
            ResourceType.BrokerService,
            ResourceType.GenericAccessResource,
        };

        /// <summary>
        /// The constructor
        /// </summary>
        public SystemRegisterService(
            ISystemRegisterRepository systemRegisterRepository,
            IResourceRegistryClient resourceRegistryClient,
            IAccessManagementClient accessManagementClient,
            ISystemChangeLogRepository systemChangeLogRepository)
        {
            _systemRegisterRepository = systemRegisterRepository;
            _resourceRegistryClient = resourceRegistryClient;
            _accessManagementClient = accessManagementClient;
            _systemChangeLogRepository = systemChangeLogRepository;
        }

        /// <inheritdoc/>
        public Task<List<RegisteredSystemResponse>> GetListRegSys(CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetAllActiveSystems(cancellation);
        }

        /// <inheritdoc/>
        public Task<List<RegisteredSystemResponse>> GetListOfSystemsForVendor(string vendorOrgNumber, CancellationToken cancellationToken = default)
        {
            return _systemRegisterRepository.GetAllSystemsForVendor(vendorOrgNumber, cancellationToken);
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
        public Task<bool> DoesClientIdExists(List<string> clientId, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.DoesClientIdExists(clientId);
        }

        /// <inheritdoc/>
        public Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, SystemChangeLog systemChangeLog, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.CreateRegisteredSystem(system, systemChangeLog, cancellation);
        }

        /// <summary>
        /// Gets the registered system's information
        /// </summary>
        /// <param name="systemId">the system id</param>
        /// <param name="cancellation">the cancellation token</param>
        /// <returns></returns>
        public Task<RegisteredSystemResponse?> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.GetRegisteredSystemById(systemId, cancellation);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.UpdateRightsForRegisteredSystem(rights, systemId, systemChangeLog, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateAccessPackagesForRegisteredSystem(List<AccessPackage> accessPackages, string systemId, SystemChangeLog systemChangeLog, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.UpdateAccessPackagesForRegisteredSystem(accessPackages, systemId, systemChangeLog, cancellation);
        }

        /// <inheritdoc/>
        public Task<bool> SetDeleteRegisteredSystemById(string id, Guid systemInternalId, SystemChangeLog systemChangeLog, CancellationToken cancellation = default)
        {
            return _systemRegisterRepository.SetDeleteRegisteredSystemById(id, systemInternalId, systemChangeLog, cancellation);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateWholeRegisteredSystem(RegisterSystemRequest updateSystem, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
        {
            return _systemRegisterRepository.UpdateRegisteredSystem(updateSystem, systemChangeLog, cancellationToken);
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
        public async Task<(List<string> InvalidFormatResourceIds, List<string> NotFoundResourceIds, List<string> NotDelegableResourceIds)> GetInvalidResourceIdsDetailed(List<Right> rights, CancellationToken cancellationToken)
        {
            ServiceResource? resource = null;
            var invalidFormatResourceIds = new List<string>();
            var notFoundResourceIds = new List<string>();
            var notDelegableResourceIds = new List<string>();
            foreach (Right right in rights)
            {
                foreach (AttributePair resourceId in right.Resource)
                {
                    string pattern = @"^urn:altinn:resource$";
                    if (!Regex.IsMatch(resourceId.Id, pattern, RegexOptions.None, TimeSpan.FromSeconds(2)))
                    {
                        invalidFormatResourceIds.Add(resourceId.Value);
                    }
                    else
                    {
                        resource = await _resourceRegistryClient.GetResource(resourceId.Value);
                        if (resource == null)
                        {
                            notFoundResourceIds.Add(resourceId.Value);
                        }
                        else if (!WhitelistedResourceTypes.Contains(resource.ResourceType))
                        {
                            notDelegableResourceIds.Add(resourceId.Value);
                        }
                    }
                }
            }

            return (invalidFormatResourceIds, notFoundResourceIds, notDelegableResourceIds);
        }

        /// <inheritdoc/>
        public async Task<bool> DoesAccessPackageExistsAndDelegable(List<AccessPackage> accessPackages, CancellationToken cancellationToken)
        {
            Package? package = null;
            foreach (AccessPackage accessPackage in accessPackages)
            {
                // get the urn value from the access package f.eks get regnskapsforer-med-signeringsrettighet from urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet
                string urnValue = accessPackage.Urn!;
                package = await _accessManagementClient.GetAccessPackage(urnValue);
                if (package == null || !package.IsDelegable)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<(List<string> InvalidFormatUrns, List<string> NotFoundUrns, List<string> NotDelegableUrns, List<string> NonAssignableUrns)>
            GetInvalidAccessPackageUrnsDetailed(List<AccessPackage> accessPackages, CancellationToken cancellationToken)
        {
            var invalidFormatUrns = new List<string>();
            var notFoundUrns = new List<string>();
            var notDelegableUrns = new List<string>();
            var nonAssignableUrns = new List<string>();

            if (accessPackages != null && accessPackages.Count > 0)
            {
                foreach (AccessPackage accessPackage in accessPackages!)
                {
                    string? urn = accessPackage.Urn;

                    if (string.IsNullOrEmpty(urn))
                    {
                        invalidFormatUrns.Add(urn ?? string.Empty);
                        continue;
                    }

                    string[] urnParts = urn.Split(':');
                    if (urnParts.Length < 4)
                    {
                        invalidFormatUrns.Add(urn);
                        continue;
                    }

                    string urnValue = urnParts[3];
                    Package? package = await _accessManagementClient.GetAccessPackage(urnValue);

                    if (package == null)
                    {
                        notFoundUrns.Add(urn);
                    }
                    else
                    {
                        if (!package.IsDelegable && !package.IsAssignable)
                        {
                            notDelegableUrns.Add(urn);
                        }

                        if (!package.IsAssignable)
                        {
                            nonAssignableUrns.Add(urn);
                        }
                    }
                }
            }

            return (invalidFormatUrns, notFoundUrns, notDelegableUrns, nonAssignableUrns);
        }

        /// <inheritdoc/>
        public Task<IList<SystemChangeLog>> GetChangeLogAsync(Guid systemInternalId, CancellationToken cancellationToken = default)
        {
            return _systemChangeLogRepository.GetChangeLogAsync(systemInternalId, cancellationToken);
        }
    }
}