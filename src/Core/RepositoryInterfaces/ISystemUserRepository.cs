using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

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
    /// <param name="userId">The id of the user who is logged in</param>
    /// <returns></returns>
    Task<Guid?> InsertSystemUser(SystemUser toBeInserted, int userId);

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
    /// Updates the IntegrationTitle (Display name) on a System User by Guid
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="integrationTitle">The Display name used in the GUI, and in the Token read by Skatteetaten</param>
    /// <returns>Number of rows affected</returns>
    Task<int> UpdateIntegrationTitle(Guid guid, string integrationTitle);

    /// <summary>
    /// Used by Maskinporten to verify a valid SystemUser during Lookup from a systemProvider
    /// </summary>
    /// <param name="clientId">The key connecting the SystemUser integration to a unique Registered System by a SystemProvider</param>
    /// <param name="systemProviderOrgNo">Used for disambiguation</param>
    /// <param name="systemUserOwnerOrgNo">The id of the end user which owns this SystemUser Integration</param>
    /// <param name="externalRef">The key connecting the SystemUser integration to a unique Customer in the SystemProvider's system</param>
    /// <param name="cancellationToken">Cancellationtoken</param>
    /// <returns></returns>
    Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, string externalRef, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of SystemUsers the Vendor has for a given system they own.
    /// </summary>
    /// <param name="systemId">The system the Vendor wants the list for</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<List<SystemUser>?> GetAllSystemUsersByVendorSystem(string systemId, long sequenceFrom, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the values for the existing system user with those from the ChangeRequest
    /// </summary>
    /// <param name="toBeChanged">SystemUser to be changed</param>
    /// <param name="userId">the user id of the reporter approving the change</param>
    /// <returns>The id (UUID) of the SystemUser</returns>
    Task<bool> ChangeSystemUser(SystemUser toBeChanged, int userId);

    /// <summary>
    /// Fetches a SystemUser by the ExternalRequestId    /// 
    /// </summary>
    /// <param name="externalRequestId">External Ref + Orgno + Systemid should uniquely define a SystemUser</param>
    /// <returns>A SystemUser, if one is active.</returns>
    Task<SystemUser?> GetSystemUserByExternalRequestId(ExternalRequestId externalRequestId);

    /// <summary>
    /// Returns a list of all SystemUsers    
    /// </summary>
    /// <returns>List of SystemUser</returns>
    Task<List<SystemUserRegisterDTO>> GetAllSystemUsers(long fromSequenceNo, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the max sequence number for SystemUsers
    /// </summary>
    /// <returns>long</returns>
    Task<long> GetMaxSystemUserSequenceNo();

    /// <summary>
    /// Returns the list of all active agent system user integration for the given party id
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <returns>List of Agent SystemUsers</returns>
    Task<List<SystemUser>> GetAllActiveAgentSystemUsersForParty(int partyId);
}