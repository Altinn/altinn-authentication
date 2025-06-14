﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Register.Models;

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
    Task<Result<RequestSystemResponse>> CreateRequest(CreateRequestSystemUser createRequest, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Gets the status based on the External Request Id 
    /// 
    /// </summary>
    /// <param name="externalRequestId">The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers. </param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<RequestSystemResponse>> GetRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the status by UUID Request Id
    /// 
    /// </summary>
    /// <param name="requestId">The Request Id as a UUID</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<RequestSystemResponse>> GetRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the status by UUID Request Id
    /// 
    /// </summary>
    /// <param name="requestId">The Request Id as a UUID</param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>The Status Response model</returns>
    Task<Result<AgentRequestSystemResponse>> GetAgentRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the Request response DTO for display in the FrontEnd
    /// </summary>
    /// <param name="party">The partyId for the end user</param>
    /// <param name="requestId">The Guid Id for the Request</param>
    /// <returns>The Request model</returns>
    Task<Result<RequestSystemResponse>> GetRequestByPartyAndRequestId(int party, Guid requestId);

    /// <summary>
    /// Approves the request and creates a system user
    /// </summary>
    /// <param name="requestId">the id of the request to be approved</param>
    /// <param name="partyId">The partyId</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The Cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> ApproveAndCreateSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Approves the request and creates a agent system user
    /// </summary>
    /// <param name="requestId">the id of the request to be approved</param>
    /// <param name="partyId">The partyId</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The Cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> ApproveAndCreateAgentSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all Requests that the Vendor has
    /// </summary>
    /// <param name="vendorOrgNo">The Vendor's organisation number, retrieved from the token</param>
    /// <param name="systemId">The registered system this listing is for, must be owned by the Vendor</param>
    /// <param name="continueRequest">The Guid denoting from where to continue with Pagination</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<Result<Page<RequestSystemResponse, Guid>>> GetAllRequestsForVendor(OrganisationNumber vendorOrgNo, string systemId, Page<Guid>.Request continueRequest, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of Status-Response-model for all agent Requests that the Vendor has
    /// </summary>
    /// <param name="vendorOrgNo">The Vendor's organisation number, retrieved from the token</param>
    /// <param name="systemId">The registered system this listing is for, must be owned by the Vendor</param>
    /// <param name="continueRequest">The Guid denoting from where to continue with Pagination</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<Result<Page<AgentRequestSystemResponse, Guid>>> GetAllAgentRequestsForVendor(OrganisationNumber vendorOrgNo, string systemId, Page<Guid>.Request continueRequest, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the request 
    /// </summary>
    /// <param name="partyId">the int valued PartyId of the reportee</param>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The cancelleation token</param>
    /// <returns>true if the request is rejected</returns>
    Task<Result<bool>> RejectSystemUser(int partyId, Guid requestId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Rejects the request for agent system user
    /// </summary>
    /// <param name="partyId">the int valued PartyId of the reportee</param>
    /// <param name="requestId">the id of the request to be rejected</param>
    /// <param name="userId">The logged in user</param>
    /// <param name="cancellationToken">The cancelleation token</param>
    /// <returns>true if the request is rejected</returns>
    Task<Result<bool>> RejectAgentSystemUser(int partyId, Guid requestId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the Vendors to delete the chosen Request by guid
    /// </summary>
    /// <returns></returns>
    Task<Result<bool>> DeleteRequestByRequestId(Guid requestId);

    /// <summary>
    /// Returns only the RedirectUrl for the Request
    /// </summary>
    /// <param name="requestId">The Request id</param>
    /// <returns></returns>
    Task<Result<string>> GetRedirectByRequestId(Guid requestId);

    /// <summary>
    /// Returns only the RedirectUrl for the Agent Request
    /// </summary>
    /// <param name="requestId">The Agent Request id</param>
    /// <returns></returns>
    Task<Result<string>> GetRedirectByAgentRequestId(Guid requestId);

    /// <summary>
    /// A Vendor can generate a new Request for a Agent-type SystemUser
    /// </summary>
    /// <param name="createAgentRequest">the request</param>
    /// <param name="vendorOrgNo">the orgno for the Vendor</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    Task<Result<AgentRequestSystemResponse>> CreateAgentRequest(CreateAgentRequestSystemUser createAgentRequest, OrganisationNumber vendorOrgNo);
    
    /// <summary>
    /// Gets the status based on the External Request Id 
    /// 
    /// </summary>
    /// <param name="externalRequestId">The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers. </param>
    /// <param name="vendorOrgNo">The OrgNo for the Vendor requesting.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    Task<Result<AgentRequestSystemResponse>> GetAgentRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo);

    /// <summary>
    /// Get the AgentRequest response DTO for display in the FrontEnd
    /// Endpoint has PEP validation of the PartyId for the end user - the Faciliator
    /// </summary>
    /// <param name="party">The partyId for the end user</param>
    /// <param name="requestId">The Guid Id for the Request</param>
    /// <returns>The AgentRequest model</returns>
    Task<Result<AgentRequestSystemResponse>> GetAgentRequestByPartyAndRequestId(int party, Guid requestId);

    /// <summary>
    /// Verifies that the logged in user has the required rights to the request, and returns the Reportee Party
    /// in the SystemUserInternal request.
    /// </summary>
    /// <param name="requestId">The id for the request</param>    
    /// <returns>RequestSystemResponseInternal</returns>
    Task<Result<RequestSystemResponseInternal>> CheckUserAuthorizationAndGetRequest(Guid requestId);

    /// <summary>
    /// Verifies that the logged in user has the required rights to the Agent request, and returns the Reportee Party
    /// in the request.
    /// </summary>
    /// <param name="requestId">The id for the request</param>    
    /// <returns>RequestSystemResponseInternal</returns>
    Task<Result<RequestSystemResponseInternal>> CheckUserAuthorizationAndGetAgentRequest(Guid requestId);
}
