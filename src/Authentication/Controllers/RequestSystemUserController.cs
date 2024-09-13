using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Authentication.Controllers;

/// <summary>
/// CRUD API for Request SystemUser
/// </summary>
[Route("authentication/api/v1/request")]
[ApiController]
public class RequestSystemUserController : ControllerBase
{
    private readonly IRequestSystemUser _requestSystemUser;

    /// <summary>
    /// Constructor
    /// </summary>
    public RequestSystemUserController(
        IRequestSystemUser requestSystemUser)
    {
        _requestSystemUser = requestSystemUser;
    }

    /// <summary>
    /// Retrieves a Registered System for the systemId.
    /// </summary>
    /// <param name="createRequest">The request model</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPost]
    public async Task<ActionResult<CreateRequestSystemUserResponse>> CreateRequest([FromBody] CreateRequestSystemUser createRequest, CancellationToken cancellationToken = default)
    {
        CreateRequestSystemUserResponse response = await _requestSystemUser.CreateRequest(createRequest);            
        return Ok(response);
    }
}