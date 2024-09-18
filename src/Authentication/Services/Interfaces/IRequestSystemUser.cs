using System;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Services.Interfaces;

/// <summary>
/// The Service that support the CRUD API for administration of SystemUser-Requests
/// </summary>
public interface IRequestSystemUser
{
    /// <summary>
    /// Create a new Request for a SystemUser
    /// 
    /// </summary>
    /// <param name="createRequest">The model describing a new Request for a SystemUser</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>Result of Response model or Problem description</returns>
    Task<Result<CreateRequestSystemUserResponse>> CreateRequest(CreateRequestSystemUser createRequest, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Gets the status based on the External Request Id 
    /// 
    /// </summary>
    /// <param name="externalRequestId">The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers. </param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<CreateRequestSystemUserResponse>> GetRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the status by UUID Request Id
    /// 
    /// </summary>
    /// <param name="requestId">The Request Id as a UUID</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<CreateRequestSystemUserResponse>> GetRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo);
}
