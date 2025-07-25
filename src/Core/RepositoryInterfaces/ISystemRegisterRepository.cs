﻿using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

/// <summary>
/// Interface to the System Register Repository
/// </summary>
public interface ISystemRegisterRepository
{
    /// <summary>
    /// Returns the list of currently available (is_deleted = false ) Registered Systems
    /// </summary>
    /// <returns>List of SystemRegister</returns>
    Task<List<RegisteredSystemResponse>> GetAllActiveSystems();

    /// <summary>
    /// Inserts a new Registered System, using an optimistic choice for the ID
    /// if the ID already exists the Insert fails, 
    /// and the calling system need to choose a different ID.
    /// The ID is a human readable string.
    /// The RegisteredSystems primary key in the db is a "hidden" Guid,
    /// making it possible to change the human readable Id.
    /// </summary>
    /// <param name="toBeInserted">The newly created Product to be inserted</param>
    /// <returns>Returns the hidden system Guid</returns>
    Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest toBeInserted, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a single RegisteredSystem, even if it was set to deleted.
    /// </summary>
    /// <param name="id">The human readable string Id</param>
    /// <returns>The Registered System</returns>
    Task<RegisteredSystemResponse?> GetRegisteredSystemById(string id);

    /// <summary>
    /// The registered systems may be renamed,
    /// this works because they also have a hidden internal id.
    /// This is useful if the first attempt to name them when
    /// registering collided with an existing name.
    /// </summary>
    /// <param name="id">The human readable string Id</param>
    /// <param name="newSystemId">The new human readable string Id</param>
    /// <returns>True if renamed</returns>
    Task<int> RenameRegisteredSystemIdByGuid(Guid id, string newSystemId);

    /// <summary>
    /// Set's the product's is_deleted column to True.
    /// This will break any existing integrations.
    /// </summary>
    /// <param name="id">The human readable string id</param>
    /// <param name="systemInternalId">The internal system idenficator for a system</param>
    /// <returns>True if set to deleted</returns>
    Task<bool> SetDeleteRegisteredSystemById(string id, Guid systemInternalId);

    /// <summary>
    /// Retrieves the list, if any, of Default Rights 
    /// the System Vendor has put on the Registered System.
    /// </summary>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>List of Default Rights</returns>
    Task<List<Right>> GetRightsForRegisteredSystem(string systemId);

    /// <summary>
    /// Retrieves the list, if any, of access packages 
    /// the System Vendor has set on the Registered System.
    /// </summary>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>List of Default Rights</returns>
    Task<List<AccessPackage>> GetAccessPackagesForRegisteredSystem(string systemId);

    /// <summary>
    /// Used for internal maintenance, the Guid is not part of any APIs
    /// </summary>
    /// <param name="id">The external string ID</param>
    /// <returns>UUID systemInternalId</returns>
    Task<Guid?> RetrieveGuidFromStringId (string id);

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
    /// <returns>true if changed</returns>
    Task<bool> UpdateAccessPackagesForRegisteredSystem(List<AccessPackage> accessPackages, string systemId);

    /// <summary>
    /// Updates the whole registered system,
    /// except internal_id, system_id and orgnr.    
    /// </summary>
    /// <returns>true if changed</returns>
    Task<bool> UpdateRegisteredSystem(RegisterSystemRequest updatedSystem, CancellationToken cancellationToken = default);

    /// Checks if the client id exists
    /// </summary>
    /// <param name="id">array of client id</param>
    /// <returns>true if one of the client id exists</returns>
    Task<bool> DoesClientIdExists(List<string> id);

    /// Gets the maskinporten clients
    /// </summary>
    /// <param name="id">array of client id</param>
    /// <returns>true if one of the client id exists</returns>
    Task<List<MaskinPortenClientInfo>> GetMaskinportenClients(List<string> id);
}
