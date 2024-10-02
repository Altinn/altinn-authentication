using System;
using System.Collections.Generic;
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
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using AltinnCore.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Constructor
    /// </summary>
    public RequestSystemUserController(
        IRequestSystemUser requestSystemUser,
        IOptions<GeneralSettings> generalSettings)
    {
        _requestSystemUser = requestSystemUser;
        _generalSettings = generalSettings.Value;
    }

    /// <summary>
    /// Route for the Created URI
    /// </summary>
    public const string CREATEDURIMIDSECTION = $"authentication/api/v1/systemuser/request/";

    /// <summary>
    /// Route for the Confirm URL on the Authn.UI that the Vendor can direct their customer to Approve the Request
    /// </summary>
    public const string CONFIRMURL = "/authfront/ui/auth/vendorrequest?id=";

    /// <summary>
    /// Route for the Get System by Vendor endpoint
    /// which uses pagination.
    /// </summary>
    public const string ROUTE_VENDOR_GET_REQUESTS_BY_SYSTEM = "vendor/bysystem";

    /// <summary>
    /// Creates a new Request based on a SystemId for a SystemUser.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]    
    [HttpPost("vendor")]
    public async Task<ActionResult<RequestSystemResponse>> CreateRequest([FromBody] CreateRequestSystemUser createRequest, CancellationToken cancellationToken = default)
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

        // Check to see if the Request already exists
        Result<RequestSystemResponse> response = await _requestSystemUser.GetRequestByExternalRef(externalRequestId, vendorOrgNo);
        if (response.IsSuccess)
        {
            return Ok(response.Value);
        }

        // This is a new Request
        response = await _requestSystemUser.CreateRequest(createRequest, vendorOrgNo);
        
        if (response.IsSuccess)
        {
            string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Value.Id;
            response.Value.ConfirmUrl = "https://authn.ui.at22.altinn.cloud" + CONFIRMURL + response.Value.Id;
            return Created(fullCreatedUri, response.Value);
        }

        return BadRequest();
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
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
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
            response.Value.ConfirmUrl = "https://authn.ui.at22.altinn.cloud" + CONFIRMURL + response.Value.Id;
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
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
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
            response.Value.ConfirmUrl = "https://authn.ui.at22.altinn.cloud" + CONFIRMURL + response.Value.Id;
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
    /// Approves the systemuser requet and creates a system user
    /// </summary>
    /// <param name="party">the partyId</param>
    /// <param name="requestId">The UUID of the request to be approved</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("{party}/{requestId}/approve")]
    public async Task<ActionResult<RequestSystemResponse>> ApproveSystemUserRequest(int party, Guid requestId, CancellationToken cancellationToken = default)
    {
        Result<bool> response = await _requestSystemUser.ApproveAndCreateSystemUser(requestId, party, cancellationToken);
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
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
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
        Result<bool> response = await _requestSystemUser.RejectSystemUser(requestId, cancellationToken);
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
}