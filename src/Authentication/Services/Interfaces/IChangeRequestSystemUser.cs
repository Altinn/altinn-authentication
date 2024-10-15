using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Services.Interfaces;

/// <summary>
/// The Service that support the CRUD API for administration of SystemUser-Requests
/// </summary>
public interface IChangeRequestSystemUser
{
    /// <summary>
    /// Get the status by UUID Request Id
    /// 
    /// </summary>
    /// <param name="requestId">The Request Id as a UUID</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<ChangeRequestResponse>> GetChangeRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the Request response DTO for display in the FrontEnd
    /// </summary>
    /// <param name="party">The partyId for the end user</param>
    /// <param name="requestId">The Guid Id for the Request</param>
    /// <returns>The Request model</returns>
    Task<Result<ChangeRequestResponse>> GetChangeRequestByPartyAndRequestId(int party, Guid requestId);

    /// <summary>
    /// Approves the request and creates a system user
    /// </summary>
    /// <param name="requestId">the id of the request to be approved</param>
    /// <param name="partyId">The partyId</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The Cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> ApproveAndDelegateChangeOnSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all Requests that the Vendor has
    /// </summary>
    /// <param name="vendorOrgNo">The Vendor's organisation number, retrieved from the token</param>
    /// <param name="systemId">The registered system this listing is for, must be owned by the Vendor</param>
    /// <param name="continueRequest">The Guid denoting from where to continue with Pagination</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<Result<Page<ChangeRequestResponse, Guid>>> GetAllChangeRequestsForVendor(OrganisationNumber vendorOrgNo, string systemId, Page<Guid>.Request continueRequest, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the request 
    /// </summary>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The cancelleation token</param>
    /// <returns>true if the request is rejected</returns>
    Task<Result<bool>> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the Vendors to delete the chosen Request by guid
    /// </summary>
    /// <returns></returns>
    Task<Result<bool>> DeleteChangeRequestByRequestId(Guid requestId);

    /// <summary>
    /// Returns only the RedirectUrl for the Request
    /// </summary>
    /// <param name="requestId">The Request id</param>
    /// <returns></returns>
    Task<Result<string>> GetRedirectByChangeRequestId(Guid requestId);

    /// <summary>
    /// Gets the status based on the External Request Id 
    /// 
    /// </summary>
    /// <param name="externalRequestId">The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers. </param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<ChangeRequestResponse>> GetChangeRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Create a new ChangeRequest for an existing SystemUser
    /// 
    /// </summary>
    /// <param name="createRequest">The model describing a new ChangeRequest for a SystemUser</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>Result of Response model or Problem description</returns>
    Task<Result<ChangeRequestResponse>> CreateChangeRequest(ChangeRequestSystemUser createRequest, OrganisationNumber vendorOrgNo);
}
