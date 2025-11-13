using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Filters;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using AltinnCore.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authentication.Controllers;
#nullable enable

/// <summary>
/// CRUD API for Request SystemUser
/// </summary>
[Route("authentication/api/v1/systemuser/request")]
[ApiController]
public class RequestSystemUserController : ControllerBase
{
    private readonly IRequestSystemUser _requestSystemUser;
    private readonly GeneralSettings _generalSettings;
    private readonly ISystemUserService _systemUserService;
    private readonly ILogger<RequestSystemUserController> _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public RequestSystemUserController(
        IRequestSystemUser requestSystemUser,
        IOptions<GeneralSettings> generalSettings,
        ISystemUserService systemUserService,
        ILogger<RequestSystemUserController> logger)
    {
        _requestSystemUser = requestSystemUser;
        _generalSettings = generalSettings.Value;
        _systemUserService = systemUserService;
        _logger = logger;
    }

    /// <summary>
    /// Route for the Created URI
    /// </summary>
    public const string CREATEDURIMIDSECTION = $"authentication/api/v1/systemuser/request/";

    /// <summary>
    /// First part of the Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Request
    /// </summary>
    public const string CONFIRMURL1 = "https://am.ui.";

    /// <summary>
    /// Second part of the Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Request
    /// </summary>
    public const string CONFIRMURL2 = "/accessmanagement/ui/systemuser/request?id=";

    /// <summary>
    /// Second part of the Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Agent Request
    /// </summary>
    public const string CONFIRMURL3 = "/accessmanagement/ui/systemuser/agentrequest?id=";

    /// <summary>
    /// Query parameter to not choose a reportee when the end user is redirected to the Authn.UI to approve the Request.
    /// </summary>
    public const string REPORTEESELECTIONPARAMETER = "&DONTCHOOSEREPORTEE=true";

    /// <summary>
    /// Route for the Get System by Vendor endpoint
    /// which uses pagination.
    /// </summary>
    public const string ROUTE_VENDOR_GET_REQUESTS_BY_SYSTEM = "vendor/bysystem";

    /// <summary>
    /// Route for the Get System by Vendor endpoint
    /// which uses pagination.
    /// </summary>
    public const string ROUTE_VENDOR_GET_AGENT_REQUESTS_BY_SYSTEM = "vendor/bysystem/agent";

    /// <summary>
    /// Creates a new Request based on a SystemId for a SystemUser.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]    
    [HttpPost("vendor")]
    [ServiceFilter(typeof(TrimStringsActionFilter))]
    public async Task<ActionResult<RequestSystemResponse>> CreateRequest([FromBody] CreateRequestSystemUser createRequest, CancellationToken cancellationToken = default)
    {
        string platform = _generalSettings.PlatformEndpoint;
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty()) 
        {
            return ProblemInstance.Create(Altinn.Authentication.Core.Problems.Problem.Vendor_Orgno_NotFound).ToActionResult();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createRequest.ExternalRef ?? createRequest.PartyOrgNo,
            OrgNo = createRequest.PartyOrgNo,
            SystemId = createRequest.SystemId,
        };
        
        SystemUserInternalDTO? existing = await _systemUserService.GetSystemUserByExternalRequestId(externalRequestId, cancellationToken);
        if (existing is not null)
        {
            return ProblemInstance.Create(Altinn.Authentication.Core.Problems.Problem.SystemUser_AlreadyExists).ToActionResult();
        }

        // Check to see if the Request already exists, and is still active ( Status is not Timed Out)
        Result<RequestSystemResponse> response = await _requestSystemUser.GetRequestByExternalRef(externalRequestId, vendorOrgNo);
        if (response.IsSuccess && response.Value.Status != RequestStatus.Timedout.ToString())
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        // This is a new Request
        response = await _requestSystemUser.CreateRequest(createRequest, vendorOrgNo);
        
        if (response.IsSuccess)
        {
            string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Value.Id;
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Created(fullCreatedUri, response.Value);
        }

        return response.Problem.ToActionResult();
    }

    /// <summary>
    /// Creates a new system user request of type agent based on a specified system ID.
    /// </summary>
    /// <param name="createAgentRequest">The request model containing details for the agent system user.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A response model containing the details of the created agent request.</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]
    [HttpPost("vendor/agent")]
    [ServiceFilter(typeof(TrimStringsActionFilter))]
    public async Task<ActionResult<AgentRequestSystemResponse>> CreateAgentRequest([FromBody] CreateAgentRequestSystemUser createAgentRequest, CancellationToken cancellationToken = default)
    {
        string platform = _generalSettings.PlatformEndpoint;
        
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return ProblemInstance.Create(Altinn.Authentication.Core.Problems.Problem.Vendor_Orgno_NotFound).ToActionResult();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createAgentRequest.ExternalRef ?? createAgentRequest.PartyOrgNo,
            OrgNo = createAgentRequest.PartyOrgNo,
            SystemId = createAgentRequest.SystemId,
        };

        SystemUserInternalDTO? existing = await _systemUserService.GetSystemUserByExternalRequestId(externalRequestId, cancellationToken);
        if (existing is not null)
        {
            return ProblemInstance.Create(Altinn.Authentication.Core.Problems.Problem.SystemUser_AlreadyExists).ToActionResult();
        }

        // Check to see if the Request already exists, and is still active ( Status is not Timed Out)
        Result<AgentRequestSystemResponse> response = await _requestSystemUser.GetAgentRequestByExternalRef(externalRequestId, vendorOrgNo);
        if (response.IsSuccess && response.Value.Status != RequestStatus.Timedout.ToString())
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL3 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        // This is a new Request
        response = await _requestSystemUser.CreateAgentRequest(createAgentRequest, vendorOrgNo);

        if (response.IsSuccess)
        {
            string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Value.Id;
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL3 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Created(fullCreatedUri, response.Value);
        }

        return response.Problem.ToActionResult();
    }

    private OrganisationNumber? RetrieveOrgNoFromToken()
    {
        string token = JwtTokenUtil.GetTokenFromContext(HttpContext, _generalSettings.JwtCookieName);
        JwtSecurityToken jwtSecurityToken = new(token);
        foreach (Claim claim in jwtSecurityToken.Claims)
        {
            // ID-porten specific claims
            if (claim.Type.Equals("consumer"))
            {
                return OrganisationNumber.CreateFromMaskinPortenToken(claim.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the Status (Response model) for a Request
    /// based only on the Request.Id GUID
    /// </summary>
    /// <param name="requestId">The UUID for the Request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/{requestId}")]
    public async Task<ActionResult<RequestSystemResponse>> GetRequestByGuid(Guid requestId, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Result<RequestSystemResponse> response = await _requestSystemUser.GetRequestByGuid(requestId, vendorOrgNo);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves the status of an agent system user request based on the request's unique identifier (UUID).
    /// </summary>
    /// <param name="requestId">The unique identifier (UUID) of the agent system user request.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An action result containing the status response model of the agent system user request.</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/agent/{requestId}")]
    public async Task<ActionResult<AgentRequestSystemResponse>> GetAgentSystemUserRequestByGuid(Guid requestId, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Result<AgentRequestSystemResponse> response = await _requestSystemUser.GetAgentRequestByGuid(requestId, vendorOrgNo);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves the Status (Response model) for a Request
    /// based on the SystemId, OrgNo and the ExternalRef 
    /// ( which is enforced as a unique combination )
    /// </summary>
    /// <param name="systemId">The Id for the chosen Registered System.</param>
    /// <param name="externalRef">The chosen external ref the Vendor sent in to the Create Request</param>
    /// <param name="orgNo">The organisation number for the customer</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/byexternalref/{systemId}/{orgNo}/{externalRef}")]
    public async Task<ActionResult<RequestSystemResponse>> GetRequestByExternalRef(string systemId, string externalRef, string orgNo, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = externalRef,
            OrgNo = orgNo,
            SystemId = systemId,
        };

        Result<RequestSystemResponse> response = await _requestSystemUser.GetRequestByExternalRef(externalRequestId, vendorOrgNo);
        
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }
        
        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        return BadRequest();
    }

    /// <summary>
    /// Retrieves the status of an agent system user request based on the unique combination of identifiers (system ID, organization number, and external reference).
    /// </summary>
    /// <param name="systemId">The ID of the registered system.</param>
    /// <param name="externalRef">The external reference provided by the vendor.</param>
    /// <param name="orgNo">The organization number of the customer.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An action result containing the status response model of the agent system user request.</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/agent/byexternalref/{systemId}/{orgNo}/{externalRef}")]
    public async Task<ActionResult<RequestSystemResponse>> GetAgentRequestByExternalRef(string systemId, string externalRef, string orgNo, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = externalRef,
            OrgNo = orgNo,
            SystemId = systemId,
        };

        Result<AgentRequestSystemResponse> response = await _requestSystemUser.GetAgentRequestByExternalRef(externalRequestId, vendorOrgNo);

        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id + REPORTEESELECTIONPARAMETER;
            return Ok(response.Value);
        }

        return BadRequest();
    }

    /// <summary>
    /// Used by the BFF to authenticate the PartyId to retrieve the chosen Request by guid
    /// </summary>
    /// <returns>RequestSystemResponse model</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [HttpGet("{party}/{requestId}")]
    public async Task<ActionResult<RequestSystemResponse>> GetRequestByPartyIdAndRequestId(int party, Guid requestId)
    {
        Result<RequestSystemResponse> res = await _requestSystemUser.GetRequestByPartyAndRequestId(party, requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Ok(res.Value);
    }

    /// <summary>
    /// Used by the BFF to retrieve the chosen Request by Guid Id alone,
    /// authorized manually via pdp that the logged in user can access the request.
    /// </summary>
    /// <returns>RequestSystemResponse model</returns>    
    [Authorize]
    [HttpGet("{requestId}")]
    public async Task<ActionResult<RequestSystemResponse>> GetRequestById(Guid requestId)
    {
        Result<RequestSystemResponseInternal> verify = await _requestSystemUser.CheckUserAuthorizationAndGetRequest(requestId);
        if (verify.IsProblem)
        {
            return verify.Problem.ToActionResult();
        }

        return Ok(verify.Value);
    }

    /// <summary>
    /// Used by the BFF to authenticate the PartyId to retrieve the chosen Request by guid
    /// Is different from the Vendor endpoint, since this authenticates the Facilitator and not the Vendor
    /// </summary>
    /// <returns>AgentRequestSystemResponse model</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [HttpGet("agent/{party}/{requestId}")]
    public async Task<ActionResult<AgentRequestSystemResponse>> GetAgentRequestByPartyIdAndRequestId(int party, Guid requestId)
    {
        Result<AgentRequestSystemResponse> res = await _requestSystemUser.GetAgentRequestByPartyAndRequestId(party, requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Ok(res.Value);
    }

    /// <summary>
    /// Used by the BFF to authenticate the PartyId to retrieve the chosen Request by guid
    /// Is different from the Vendor endpoint, since this authenticates the Facilitator and not the Vendor
    /// </summary>
    /// <returns>AgentRequestSystemResponse model</returns>
    [Authorize]
    [HttpGet("agent/{requestId}")]
    public async Task<ActionResult<RequestSystemResponseInternal>> GetAgentRequestById(Guid requestId)
    {
        Result<RequestSystemResponseInternal> verify = await _requestSystemUser.CheckUserAuthorizationAndGetAgentRequest(requestId);
        if (verify.IsProblem)
        {
            return verify.Problem.ToActionResult();
        }

        return Ok(verify.Value);
    }

    /// <summary>
    /// Approves the systemuser requet and creates a system user
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]    
    [HttpPost("{party}/{requestId}/approve")]
    public async Task<ActionResult<RequestSystemResponse>> ApproveSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.ApproveAndCreateSystemUser(requestId, party, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Escalates the Approval of the systemuser request, since the logged in user lack the AccessManager Role
    /// The request is forwarded to the Portal where it will be visible for users with the AccessManager Role
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("{party}/{requestId}/escalate")]
    public async Task<ActionResult<bool>> EscalateApprovalSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.EscalateApprovalSystemUser(requestId, party, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Approves the systemuser request of type agent and creates a system user of type agent
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("agent/{party}/{requestId}/approve")]
    public async Task<ActionResult<AgentRequestSystemResponse>> ApproveAgentSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.ApproveAndCreateAgentSystemUser(requestId, party, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Escalates the Approval of the Agent Systemuser request, since the logged in user lack the AccessManager Role
    /// The request is forwarded to the Portal where it will be visible for users with the AccessManager Role
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("agent/{party}/{requestId}/escalate")]
    public async Task<ActionResult<AgentRequestSystemResponse>> EscalateApprovalAgentSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.EscalateApprovalAgentSystemUser(requestId, party, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves a list of Status-Response-model for all Requests that the Vendor has for a given system they own.
    /// </summary>
    /// <param name="systemId">The system the Vendor wants the list for</param>
    /// <param name="token">Optional continuation token</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/bysystem/{systemId}", Name = ROUTE_VENDOR_GET_REQUESTS_BY_SYSTEM)]
    public async Task<ActionResult<Paginated<RequestSystemResponse>>> GetAllRequestsForVendor(
        string systemId,
        [FromQuery(Name = "token")] Opaque<Guid>? token = null,
        CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Page<Guid>.Request continueFrom = null!;
        if (token?.Value is not null)
        {
            continueFrom = Page.ContinueFrom(token!.Value);
        }

        Result<Page<RequestSystemResponse, Guid>> pageResult =
          await _requestSystemUser.GetAllRequestsForVendor(
              vendorOrgNo, systemId, continueFrom, cancellationToken);
        if (pageResult.IsProblem)
        {
            return pageResult.Problem.ToActionResult();
        }

        var nextLink = pageResult.Value.ContinuationToken.HasValue
            ? Url.Link(ROUTE_VENDOR_GET_REQUESTS_BY_SYSTEM, new
            {
                systemId,
                token = Opaque.Create(pageResult.Value.ContinuationToken.Value)
            })
            : null;

        if (pageResult.IsSuccess)
        {
            return Paginated.Create(pageResult.Value.Items.ToList(), nextLink);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves a paginated list of all system user requests of type agent for a given system owned by the vendor.
    /// </summary>
    /// <param name="systemId">The ID of the system the Vendor owns</param>
    /// <param name="token">Optional continuation token</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An action result containing a paginated list of agent system user requests</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_READ)]
    [HttpGet("vendor/agent/bysystem/{systemId}", Name = ROUTE_VENDOR_GET_AGENT_REQUESTS_BY_SYSTEM)]
    public async Task<ActionResult<Paginated<AgentRequestSystemResponse>>> GetAllAgentRequestsForVendor(
        string systemId,
        [FromQuery(Name = "token")] Opaque<Guid>? token = null,
        CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Page<Guid>.Request continueFrom = null!;
        if (token?.Value is not null)
        {
            continueFrom = Page.ContinueFrom(token!.Value);
        }

        Result<Page<AgentRequestSystemResponse, Guid>> pageResult =
          await _requestSystemUser.GetAllAgentRequestsForVendor(
              vendorOrgNo, systemId, continueFrom, cancellationToken);
        if (pageResult.IsProblem)
        {
            return pageResult.Problem.ToActionResult();
        }

        var nextLink = pageResult.Value.ContinuationToken.HasValue
            ? Url.Link(ROUTE_VENDOR_GET_AGENT_REQUESTS_BY_SYSTEM, new
            {
                systemId,
                token = Opaque.Create(pageResult.Value.ContinuationToken.Value)
            })
            : null;

        if (pageResult.IsSuccess)
        {
            return Paginated.Create(pageResult.Value.Items.ToList(), nextLink);
        }

        return NotFound();
    }

    /// <summary>
    /// Rejects the systemuser request
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be rejected</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("{party}/{requestId}/reject")]
    public async Task<ActionResult<RequestSystemResponse>> RejectSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.RejectSystemUser(party, requestId, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Get an unpaginated list of the Pending Standard Requests
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="orgno">the party org no, in string format</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpGet("{party}/{orgno}/pending")]
    public async Task<ActionResult<List<RequestSystemResponse>>> GetPendingStandardRequests(int party, string orgno, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<List<RequestSystemResponse>> response = await _requestSystemUser.GetPendingStandardRequests(orgno, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Get an unpaginated list of the Pending Agent Requests
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="orgno">the party org no, in string format</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpGet("agent/{party}/{orgno}/pending")]
    public async Task<ActionResult<List<AgentRequestSystemResponse>>> GetPendingAgentRequests(int party, string orgno, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<List<AgentRequestSystemResponse>> response = await _requestSystemUser.GetPendingAgentRequests(orgno, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Rejects the systemuser request of type agent
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be rejected</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("agent/{party}/{requestId}/reject")]
    public async Task<ActionResult<bool>> RejectAgentSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _requestSystemUser.RejectAgentSystemUser(party, requestId, userId, cancellationToken);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return NotFound();
    }

    /// <summary>
    /// Used by the Vendors to delete the chosen Request by guid
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]
    [HttpDelete("vendor/{requestId}")]
    public async Task<ActionResult<RequestSystemResponse>> DeleteRequestByRequestId(Guid requestId)
    {
        Result<bool> res = await _requestSystemUser.DeleteRequestByRequestId(requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Accepted();
    }
}