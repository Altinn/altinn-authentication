using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

public interface IRequestRepository
{
    /// <summary>
    /// Inserts a new CreateRequest into the db
    /// </summary>
    /// <param name="createRequest">The validated Create Request model from the Service layer</param>
    /// <returns>The same Request model</returns>
    Task<Result<bool>> CreateRequest(RequestSystemResponse createRequest);

    /// <summary>
    /// Gets a Request model by the internal Guid ( which later is repurposed as the SystemUser Id )
    /// </summary>
    /// <param name="internalId">Internal Request guid</param>
    /// <returns>Create Request model</returns>
    Task<RequestSystemResponse?> GetRequestByInternalId (Guid internalId);

    /// <summary>
    /// Gets a Request model by the internal Guid ( which later is repurposed as the SystemUser Id )
    /// </summary>
    /// <param name="internalId">Internal Request guid</param>
    /// <returns>Create Agent Request model</returns>
    Task<AgentRequestSystemResponse?> GetAgentRequestByInternalId(Guid internalId);

    /// <summary>
    /// Gets a Request model by the three external references
    /// <param name="externalRequestId">Struct containing the three external references</param>
    /// <returns>Create Request model</returns>
    Task<RequestSystemResponse?> GetRequestByExternalReferences(ExternalRequestId externalRequestId);

    /// <summary>
    /// Approves a system user request and creates a new system user
    /// </summary>
    /// <param name="requestId">the id of the request to be accepted</param>
    /// <param name="toBeInserted">the system user to be created</param>
    /// <param name="userId">the logged in user</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns>returns the system user id</returns>
    Task<Guid?> ApproveAndCreateSystemUser(Guid requestId, SystemUser toBeInserted, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all Requests that the Vendor has
    /// </summary>    
    /// <param name="systemId">The chosen system</param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns></returns>
    Task<List<RequestSystemResponse>> GetAllRequestsBySystem(string systemId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all agent Requests that the Vendor has
    /// </summary>    
    /// <param name="systemId">The chosen system</param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns>list of agent requests for a system the vendor has</returns>
    Task<List<AgentRequestSystemResponse>> GetAllAgentRequestsBySystem(string systemId, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the system user request
    /// </summary>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="userId">the logged in user</param>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the system user request is updated as rejected</returns>
    Task<bool> RejectSystemUser(Guid requestId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the Vendors to delete the chosen Request by guid
    /// </summary>
    /// <returns></returns>
    Task<bool> DeleteRequestByRequestId(Guid requestId);

    /// <summary>
    /// Soft deletes all requests older than the configured timeout in the main table
    /// </summary>
    /// <returns></returns>
    Task<int> SetDeleteTimedoutRequests(int days);

    /// <summary>
    /// Copies all requests older than the configured timeout to the archive table
    /// </summary>
    /// <returns></returns>
    Task<int> CopyOldRequestsToArchive(int days);

    /// <summary>
    /// Hard deletes all archived and soft-deleted requests from the main table
    /// </summary>
    /// <returns></returns>
    Task<int> DeleteArchivedAndDeleted(int days);

    /// <summary>
    /// Not reachable from the API
    /// </summary>
    /// <param name="internalId">The guid as it was in the main tabble</param>
    /// <returns></returns>
    Task<RequestSystemResponse?> GetArchivedRequestByInternalId(Guid internalId);

    /// <summary>
    /// Inserts a new CreateRequest into the db
    /// </summary>
    /// <param name="createRequest">The validated Create Request model from the Service layer</param>
    /// <returns>The same Request model</returns>
    Task<Result<bool>> CreateAgentRequest(AgentRequestSystemResponse created);

    /// <summary>
    /// Gets a Request model by the three external references
    /// <param name="externalRequestId">Struct containing the three external references</param>
    /// <returns>Create Request model</returns>
    Task<AgentRequestSystemResponse?> GetAgentRequestByExternalReferences(ExternalRequestId externalRequestId);
}
