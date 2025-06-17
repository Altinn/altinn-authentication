using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Authentication.Controllers;
#nullable enable

/// <summary>
/// CRUD API for Request SystemUser
/// </summary>
[Route("authentication/api/v1/systemuser/changerequest")]
[ApiController]
public class ChangeRequestSystemUserController : ControllerBase
{
    private readonly IChangeRequestSystemUser _changeRequestService;
    private readonly GeneralSettings _generalSettings;

    /// <summary>
    /// Constructor
    /// </summary>
    public ChangeRequestSystemUserController(
        IChangeRequestSystemUser changeRequestService,
        IOptions<GeneralSettings> generalSettings)
    {
        _changeRequestService = changeRequestService;
        _generalSettings = generalSettings.Value;
    }

    /// <summary>
    /// Route for the Created URI
    /// </summary>
    public const string CREATEDURIMIDSECTION = $"authentication/api/v1/systemuser/changerequest/";

    /// <summary>
    /// First part of the Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Request
    /// </summary>
    public const string CONFIRMURL1 = "https://am.ui.";

    /// <summary>
    /// Second part of the Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Request
    /// </summary>
    public const string CONFIRMURL2 = "/accessmanagement/ui/systemuser/changerequest?id=";

    /// <summary>
    /// Route for the Get System by Vendor endpoint
    /// which uses pagination.
    /// </summary>
    public const string ROUTE_VENDOR_GET_REQUESTS_BY_SYSTEM = "vendor/changerequest/bysystem";

    /// <summary>
    /// Verifies if the given set(s) of required and/or unwanted rights are delegated for the given systemId and user.
    /// </summary>
    /// <param name="validateSet">The model containing the set(s) of required and/or unwanted rights</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]
    [HttpPost("vendor/verify")]
    public async Task<ActionResult<ChangeRequestResponse>> VerifySetOfRights([FromBody] ChangeRequestSystemUser validateSet, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        // Check to see if both the Required and Unwanted Rights are empty
        var emptyResponse = EmptySetsReturnEmptyResponse(validateSet);
        if (emptyResponse is not null)
        {
            return Ok(emptyResponse);
        }

        // Calls the PDP endpoint to validate the set of rights
        Result<ChangeRequestResponse> response = await _changeRequestService.VerifySetOfRights(validateSet, vendorOrgNo);
        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        return response.Problem.ToActionResult();
    }

    /// <summary>
    /// Creates a new Request based on a SystemId for a SystemUser.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]    
    [HttpPost("vendor")]
    public async Task<ActionResult<ChangeRequestResponse>> CreateChangeRequest([FromBody] ChangeRequestSystemUser createRequest, CancellationToken cancellationToken = default)
    {
        string platform = _generalSettings.PlatformEndpoint;
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty()) 
        {
            return Unauthorized();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createRequest.ExternalRef ?? createRequest.PartyOrgNo,
            OrgNo = createRequest.PartyOrgNo,
            SystemId = createRequest.SystemId,
        };

        // Check to see if both the Required and Unwanted Rights are empty
        var emptyResponse = EmptySetsReturnEmptyResponse(createRequest);
        if (emptyResponse is not null)
        {
            return Ok(emptyResponse);
        }

        // Check to see if the Request already exists
        Result<ChangeRequestResponse> response = await _changeRequestService.GetChangeRequestByExternalRef(externalRequestId, vendorOrgNo);
        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id;
            return Ok(response.Value);
        }

        // This is a new Request
        response = await _changeRequestService.CreateChangeRequest(createRequest, vendorOrgNo);
        
        if (response.IsSuccess)
        {
            string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Value.Id;
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id;
            return Created(fullCreatedUri, response.Value);
        }

        return response.Problem.ToActionResult();
    }

    private static ChangeRequestResponse? EmptySetsReturnEmptyResponse(ChangeRequestSystemUser createRequest)
    {
        if (createRequest.RequiredRights.Count == 0 && createRequest.UnwantedRights.Count == 0)
        {
            return new ChangeRequestResponse
            {
                Id = Guid.Empty,
                ExternalRef = createRequest.ExternalRef ?? createRequest.PartyOrgNo,
                SystemId = createRequest.SystemId,
                SystemUserId = Guid.Empty,
                PartyOrgNo = createRequest.PartyOrgNo,
                RequiredRights = [],
                UnwantedRights = [],
                Status = "new",
                RedirectUrl = createRequest.RedirectUrl
            };
        }

        return null;
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
    public async Task<ActionResult<ChangeRequestResponse>> GetChangeRequestByGuid(Guid requestId, CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Result<ChangeRequestResponse> response = await _changeRequestService.GetChangeRequestByGuid(requestId, vendorOrgNo);
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }

        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id;
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
    public async Task<ActionResult<ChangeRequestResponse>> GetChangeRequestByExternalRef(string systemId, string externalRef, string orgNo, CancellationToken cancellationToken = default)
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

        Result<ChangeRequestResponse> response = await _changeRequestService.GetChangeRequestByExternalRef(externalRequestId, vendorOrgNo);
        
        if (response.IsProblem)
        {
            return response.Problem.ToActionResult();
        }
        
        if (response.IsSuccess)
        {
            response.Value.ConfirmUrl = CONFIRMURL1 + _generalSettings.HostName + CONFIRMURL2 + response.Value.Id;
            return Ok(response.Value);
        }

        return BadRequest();
    }

    /// <summary>
    /// Used by the BFF to authenticate the PartyId to retrieve the chosen Request by guid
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [HttpGet("{party}/{requestId}")]
    public async Task<ActionResult<ChangeRequestResponse>> GetRequestByPartyIdAndRequestId(int party, Guid requestId)
    {
        Result<ChangeRequestResponse> res = await _changeRequestService.GetChangeRequestByPartyAndRequestId(party, requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Ok(res.Value);
    }

    /// <summary>
    /// Used by the BFF to authenticate the userId to retrieve the chosen Request by guid without partyId
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("{requestId}")]
    public async Task<ActionResult<ChangeRequestResponseInternal>> GetChangeRequestById(Guid requestId)
    {
        Result<ChangeRequestResponseInternal> res = await _changeRequestService.CheckUserAuthorizationAndGetRequest(requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Ok(res.Value);
    }

    /// <summary>
    /// Approves the systemuser requet and creates a system user
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("{party}/{requestId}/approve")]
    public async Task<ActionResult<ChangeRequestResponse>> ApproveSystemUserChangeRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _changeRequestService.ApproveAndDelegateChangeOnSystemUser(requestId, party, userId, cancellationToken);
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
    public async Task<ActionResult<Paginated<ChangeRequestResponse>>> GetAllChangeRequestsForVendor(
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

        Result<Page<ChangeRequestResponse, Guid>> pageResult =
          await _changeRequestService.GetAllChangeRequestsForVendor(
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
    /// Rejects the systemuser request
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be rejected</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("{party}/{requestId}/reject")]
    public async Task<ActionResult<ChangeRequestResponse>> RejectSystemUserChangeRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        int userId = AuthenticationHelper.GetUserId(HttpContext);
        Result<bool> response = await _changeRequestService.RejectChangeOnSystemUser(requestId, userId, cancellationToken);
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
    public async Task<ActionResult<ChangeRequestResponse>> DeleteChangeRequestByRequestId(Guid requestId)
    {
        Result<bool> res = await _changeRequestService.DeleteChangeRequestByRequestId(requestId);
        if (res.IsProblem)
        {
            return res.Problem.ToActionResult();
        }

        return Accepted();
    }
}