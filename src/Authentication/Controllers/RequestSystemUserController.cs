﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
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
    /// Creates a new Request based on a SystemId for a SystemUser.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    //[Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPost]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> CreateRequest([FromBody] CreateRequestSystemUser createRequest, CancellationToken cancellationToken = default)
    {
        string platform = _generalSettings.PlatformEndpoint;
        string? vendorOrgNo = "123456789";//RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == string.Empty) 
        {
            return BadRequest();
        }

        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createRequest.ExternalRef ?? createRequest.PartyOrgNo,
            OrgNo = createRequest.PartyOrgNo,
            SystemId = createRequest.SystemId,
        };

        // Check to see if the Request already exists
        CreateRequestSystemUserResponse? response = (await _requestSystemUser.GetRequestByExternalRef(externalRequestId)).Value;
        if (response is not null)
        {
            return Ok(response);
        }

        // This is a new Request
        response = (await _requestSystemUser.CreateRequest(createRequest, vendorOrgNo)).Value;
        
        if (response is not null)
        {
            string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Id;
            return Created(fullCreatedUri, response);
        }

        return BadRequest();
    }

    private string? RetrieveOrgNoFromToken()
    {
        string token = JwtTokenUtil.GetTokenFromContext(HttpContext, _generalSettings.JwtCookieName);
        JwtSecurityToken jwtSecurityToken = new(token);
        foreach (Claim claim in jwtSecurityToken.Claims)
        {
            // ID-porten specific claims
            if (claim.Type.Equals("pid"))
            {
                return claim.Value;
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
    //[Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("{requestId}")]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> GetRequestByGuid(Guid requestId, CancellationToken cancellationToken = default)
    {
        CreateRequestSystemUserResponse? response = (await _requestSystemUser.GetRequestByGuid(requestId)).Value;
        if (response is not null)
        {
            return Ok(response);
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
    //[Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("{systemId}/{orgNo}/{externalRef}")]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> GetRequestByExternalRef(string systemId, string externalRef, string orgNo, CancellationToken cancellationToken = default)
    {
        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = externalRef,
            OrgNo = orgNo,
            SystemId = systemId,
        };

        CreateRequestSystemUserResponse? response = (await _requestSystemUser.GetRequestByExternalRef(externalRequestId)).Value;
        if (response is not null)
        {
            return Ok(response);
        }

        return NotFound();
    }
}