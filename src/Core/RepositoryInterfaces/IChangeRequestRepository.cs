using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

public interface IChangeRequestRepository
{
    /// <summary>
    /// Inserts a new ChangeRequest into the db
    /// </summary>
    /// <param name="createRequest">The validated Create Request model from the Service layer</param>
    /// <returns>The same Request model</returns>
    Task<Result<bool>> CreateChangeRequest(ChangeRequestResponse createRequest);

    /// <summary>
    /// Gets a ChangeRequest model by the internal Guid ( which later is repurposed as the SystemUser Id )
    /// </summary>
    /// <param name="internalId">Internal Request guid</param>
    /// <returns>Create Request model</returns>
    Task<ChangeRequestResponse?> GetChangeRequestByInternalId (Guid internalId);

    /// <summary>
    /// Gets a ChangeRequest model by the three external references
    /// <param name="externalRequestId">Struct containing the three external references</param>
    /// <returns>Create Request model</returns>
    Task<ChangeRequestResponse?> GetChangeRequestByExternalReferences(ExternalRequestId externalRequestId);

    /// <summary>
    /// Logs the Approval of a ChangeRequest and sets the changed by field on the SystemUser
    /// </summary>
    /// <param name="requestId">the id of the request to be accepted</param>
    /// <param name="toBeInserted">the system user to be created</param>
    /// <param name="userId">the logged in user</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns>true or false</returns>
    Task<bool> PersistApprovalOfChangeRequest(Guid requestId, SystemUser toBeInserted, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all ChangeRequests that the Vendor has
    /// </summary>    
    /// <param name="systemId">The chosen system</param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns></returns>
    Task<List<ChangeRequestResponse>> GetAllChangeRequestsBySystem(string systemId, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the system user ChangeRequest
    /// </summary>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="userId">the logged in user</param>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the system user request is updated as rejected</returns>
    Task<bool> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the Vendors to delete the chosen ChangeRequest by guid
    /// </summary>
    /// <returns></returns>
    Task<bool> DeleteChangeRequestByRequestId(Guid requestId);

    /// <summary>
    /// Deletes all change requests older than the configured timeout
    /// </summary>
    /// <returns></returns>
    Task<int> DeleteTimedoutChangeRequests();

    /// <summary>
    /// Gets a ChangeRequest model by the SystemUserId
    /// </summary>
    /// <param name="systemUserId"></param>
    /// <returns></returns>
    Task<ChangeRequestResponse?> GetChangeRequestBySystemUserId(Guid systemUserId);
}
