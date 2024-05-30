using Altinn.Platform.Authentication.Core.Models;

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

    /// <summary>
    /// Updates the Product Name on an Integration by Guid
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="productName"></param>
    /// <returns>Number of rows affected</returns>
    Task<int> UpdateProductName(Guid guid, string productName);

    /// <summary>
    /// Used by Maskinporten to verify a valid SystemUser during Lookup from a systemProvider
    /// </summary>
    /// <param name="clientId">The key connecting the SystemUser integration to a unique Registered System by a SystemProvider</param>
    /// <param name="systemProviderOrgNo">Used for disambiguation</param>
    /// <param name="systemUserOwnerOrgNo">The id of the end user which owns this SystemUser Integration</param>
    /// <param name="cancellationToken">Cancellationtoken</param>
    /// <returns></returns>
    Task<SystemUser?> CheckIfPartyHasIntegrationByOrgNo(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, CancellationToken cancellationToken);
}