using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;

#nullable enable

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// The Interface for the System Register Service
    /// </summary>
    public interface ISystemRegisterService
    {
        /// <summary>
        /// Retrieves the list of all Registered Systems, except those marked as deleted.
        /// </summary>
        /// <param name="cancellation">The cancellation token</param>
        /// <returns></returns>
        Task<List<RegisteredSystemResponse>> GetListRegSys(CancellationToken cancellation = default);

        /// <summary>
        /// Retrieves the list, if any, of the Default Rights the System Provider
        /// has set for the Registered System.
        /// </summary>
        /// <param name="systemId">The Id of the Registered System</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>List of Default Rights</returns>
        Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default);

        /// <summary>
        /// Retrieves the list, if any, of the access packages the System Provider
        /// has set for the Registered System.
        /// </summary>
        /// <param name="systemId">The Id of the Registered System</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>List of access packages for the registered system</returns>
        Task<List<AccessPackage>> GetAccessPackagesForRegisteredSystem(string systemId, CancellationToken cancellation = default);

        /// <summary>
        /// Retrieves the list, if any, of the Default Rights the System Provider
        /// has set for the Registered System.
        /// </summary>
        /// <param name="systemId">The Id of the Registered System</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>List of Default Rights</returns>
        Task<RegisteredSystemResponse?> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default);

        /// <summary>
        /// Inserts a new unique ClientId
        /// </summary>
        /// <param name="clientId">The Client_Ids are maintained by Maskinporten, but we need to reference them in the db</param>
        /// <param name="systemInteralId">the internal system idenficator for a system</param>
        /// <param name="cancellationToken">The Cancellationtoken</param>
        /// <returns></returns>
        Task<bool> CreateClient(string clientId, Guid systemInteralId, CancellationToken cancellationToken);

        /// <summary>
        /// Inserts a new Registered System
        /// </summary>
        /// <param name="system">The descriptor DTO for a new System</param>
        /// <param name="cancellation">The Cancelation token</param>
        /// <returns></returns>
        Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, CancellationToken cancellation = default);

        /// <summary>
        /// Updates the rights on a registered system
        /// </summary>
        /// <param name="rights">A list of rights</param>
        /// <param name="systemId">The human readable string id</param>
        /// <returns>true if changed</returns>
        Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId);

        /// <summary>
        /// Updates the access packages on a registered system
        /// </summary>
        /// <param name="accessPackages">A list of access packages</param>
        /// <param name="systemId">The human readable string id</param>
        /// <param name="cancellation">The cancellation token</param>
        /// <returns>true if changed</returns>
        Task<bool> UpdateAccessPackagesForRegisteredSystem(List<AccessPackage> accessPackages, string systemId, CancellationToken cancellation = default);

        /// <summary>
        /// Set's the product's is_deleted column to True.
        /// Deletes the clientid from the maskinporten_cleitn table
        /// This will break any existing integrations.
        /// </summary>
        /// <param name="id">The human readable string id</param>
        /// <param name="systemInternalId">The internal system idenficator for a system</param>
        /// <returns>True if set to deleted</returns>
        Task<bool> SetDeleteRegisteredSystemById(string id, Guid systemInternalId);

        /// <summary>
        /// Replaces the entire registered system
        /// </summary>
        /// <param name="updateSystem">The updated system model</param>
        /// <param name="systemId">The Id of the Registered System </param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        Task<bool> UpdateWholeRegisteredSystem(RegisterSystemRequest updateSystem, string systemId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if one of the clientid exists
        /// </summary>
        /// <param name="clientId">the maskinporten client id</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>true when one of the client id already exists</returns>
        Task<bool> DoesClientIdExists(List<string> clientId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if one of the clientid exists
        /// </summary>
        /// <param name="clientId">the maskinporten client id</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>true when one of the client id already exists</returns>
        Task<List<MaskinPortenClientInfo>> GetMaskinportenClients(List<string> clientId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the resourceids are found in resource register
        /// </summary>
        /// <param name="rights">the list of resource ids</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>false when one of the resource idnot found</returns>
        Task<bool> DoesResourceIdExists(List<Right> rights, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the resourceids are found in resource register
        /// </summary>
        /// <param name="accessPackages">access packages</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>false when one of the resource idnot found</returns>
        Task<bool> DoesAccessPackageExistsAndDelegable(List<AccessPackage> accessPackages, CancellationToken cancellationToken);
    }
}
