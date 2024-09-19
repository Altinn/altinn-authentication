using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

public interface IRequestRepository
{
    /// <summary>
    /// Inserts a new CreateRequest into the db
    /// </summary>
    /// <param name="createRequest">The validated Create Request model from the Service layer</param>
    /// <returns>The same Request model</returns>
    Task<Result<bool>> CreateRequest(CreateRequestSystemUserResponse createRequest);

    /// <summary>
    /// Gets a Request model by the internal Guid ( which later is repurposed as the SystemUser Id )
    /// </summary>
    /// <param name="internalId">Internal Request guid</param>
    /// <returns>Create Request model</returns>
    Task<CreateRequestSystemUserResponse?> GetRequestByInternalId (Guid internalId);

    /// <summary>
    /// Gets a Request model by the three external references
    /// <param name="externalRequestId">Struct containing the three external references</param>
    /// <returns>Create Request model</returns>
    Task<CreateRequestSystemUserResponse?> GetRequestByExternalReferences(ExternalRequestId externalRequestId);

    Task<Guid?> ApproveAndCreateSystemUser(Guid requestId, SystemUser toBeInserted, CancellationToken cancellationToken);
}
