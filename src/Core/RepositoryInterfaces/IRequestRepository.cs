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
    /// Gets a Request model by the three external references
    /// <param name="externalRequestId">Struct containing the three external references</param>
    /// <returns>Create Request model</returns>
    Task<RequestSystemResponse?> GetRequestByExternalReferences(ExternalRequestId externalRequestId);

    /// <summary>
    /// Approves a system user request and creates a new system user
    /// </summary>
    /// <param name="requestId">the id of the request to be accepted</param>
    /// <param name="toBeInserted">the system user to be created</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns>returns the system user id</returns>
    Task<Guid?> ApproveAndCreateSystemUser(Guid requestId, SystemUser toBeInserted, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all Requests that the Vendor has
    /// </summary>    
    /// <param name="systemId">The chosen system</param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns></returns>
    Task<List<RequestSystemResponse>> GetAllRequestsBySystem(string systemId, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the system user request
    /// </summary>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the system user request is updated as rejected</returns>
    Task<bool> RejectSystemUser(Guid requestId, CancellationToken cancellationToken);
}
