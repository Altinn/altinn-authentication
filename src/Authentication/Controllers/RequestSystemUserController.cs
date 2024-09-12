using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
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
    private readonly ISystemRegisterService _systemRegisterService;
    private readonly ISystemUserService _systemUserService;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemRegisterService">The Service for Registered Systems</param>
    /// <param name="systemUserService">The Service for SystemUsers</param>
    public RequestSystemUserController(
        ISystemRegisterService systemRegisterService,
        ISystemUserService systemUserService)
    {
        _systemRegisterService = systemRegisterService;
        _systemUserService = systemUserService;
    }

    /// <summary>
    /// Retrieves a Registered System for the systemId.
    /// </summary>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPost("{systemId}")]
    public async Task<ActionResult<RegisterSystemResponse>> GetRegisteredSystemInfo(string systemId, CancellationToken cancellationToken = default)
    {
        RegisterSystemResponse registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
        
        return Ok(registeredSystem);
    }

}