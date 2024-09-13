using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;
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
    public const string CREATEDURIMIDSECTION = $"authentication/api/v1/systemuser/request";

    /// <summary>
    /// Creates a new Request based on a SystemId for a SystemUser.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Response model of CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPost]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> CreateRequest([FromBody] CreateRequestSystemUser createRequest, CancellationToken cancellationToken = default)
    {
        string platform = _generalSettings.PlatformEndpoint;

        CreateRequestSystemUserResponse? response = await _requestSystemUser.GetRequestByExternalRef(createRequest.SystemId, createRequest.ExternalRef);
        if (response is not null)
        {
            return Ok(response);
        }

        response = await _requestSystemUser.CreateRequest(createRequest);

        string fullCreatedUri = platform + CREATEDURIMIDSECTION + response.Id;

        if (response is not null)
        {
            return Created(fullCreatedUri, response);
        }

        return BadRequest();
    }

    /// <summary>
    /// Retrieves the Status (Response model) for a Request
    /// based only on the Request.Id GUID
    /// </summary>
    /// <param name="requestId">The UUID for the Request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("{requestId}")]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> GetRequestByGuid(Guid requestId, CancellationToken cancellationToken = default)
    {
        CreateRequestSystemUserResponse? response = await _requestSystemUser.GetRequestByGuid(requestId);
        if (response is not null)
        {
            return Ok(response);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves the Status (Response model) for a Request
    /// based both on the SystemId and the ExternalRef 
    /// ( which is enforced as a unique combination )
    /// </summary>
    /// <param name="systemId">The Id for the chosen Registered System.</param>
    /// <param name="externalRef">The chosen external ref the Vendor sent in to the Create Request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("{systemId}/{externalRef}")]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> GetRequestByExsternalRef(string systemId, string externalRef, CancellationToken cancellationToken = default)
    {
        CreateRequestSystemUserResponse? response = await _requestSystemUser.GetRequestByExternalRef(systemId, externalRef);
        if (response is not null)
        {
            return Ok(response);
        }

        return NotFound();
    }
}