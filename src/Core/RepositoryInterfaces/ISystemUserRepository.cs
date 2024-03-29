﻿using Altinn.Platform.Authentication.Core.Models;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

/// <summary>
/// Repository Interface, to be implemented in the Persistance layer
/// </summary>
public interface ISystemUserRepository
{
    /// <summary>
    /// Insert the descriptor of the new SystemUser, returns the persisted SystemUser with a db generated Entity ID
    /// </summary>
    /// <param name="toBeInserted">The desciptor of the new SystemUser to be inserted, has either a null ID, or an ID provided by the frontend, the db generate a UUID if none is provided.</param>
    /// <returns></returns>
    Task<Guid> InsertSystemUser(SystemUser toBeInserted);

    /// <summary>
    /// Returns the list of all active system user integration for the given party id
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <returns></returns>
    Task<List<SystemUser>> GetAllActiveSystemUsersForParty(int partyId);

    /// <summary>
    /// Returns a single System User integration by its id
    /// </summary>
    /// <param name="id">The Guid id</param>
    /// <returns>Returns a System User Integration</returns>
    Task<SystemUser?> GetSystemUserById(Guid id);

    /// <summary>
    /// Sets the id'ed System User Integration's IsDeleted flag to true in the db, and returns true if it succeeds
    /// </summary>
    /// <param name="id">The Guid id</param>
    Task SetDeleteSystemUserById(Guid id);
}