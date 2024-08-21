﻿using System;
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
/// CRUD API for SystemRegister
/// </summary>
[Route("authentication/api/v1/systemregister")]
[ApiController]
public class SystemRegisterController : ControllerBase
{
    private readonly ISystemRegisterService _systemRegisterService;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemRegisterService">The Service</param>
    public SystemRegisterController(ISystemRegisterService systemRegisterService)
    {
        _systemRegisterService = systemRegisterService;
    }

    /// <summary>
    /// Retrieves the List of all the Registered Systems, except those marked as deleted.
    /// </summary>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns></returns>    
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet]
    public async Task<ActionResult<List<RegisterSystemResponse>>> GetListOfRegisteredSystems(CancellationToken cancellationToken = default)
    {
        List<RegisterSystemResponse> lista = [];

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        return Ok(lista);
    }

    /// <summary>
    /// Retrieves a Registered System for the systemId.
    /// </summary>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    [HttpGet("system/{systemId}")]
    public async Task<ActionResult<RegisterSystemResponse>> GetRegisteredSystemInfo(string systemId, CancellationToken cancellationToken = default)
    {
        RegisterSystemResponse registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
        
        return Ok(registeredSystem);
    }

    /// <summary>
    /// Replaces the entire registered system
    /// </summary>
    /// <param name="updateSystem">The updated system model</param>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    [HttpPut("system/{systemId}")]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateWholeRegisteredSystem([FromBody] RegisterSystemRequest updateSystem, string systemId, CancellationToken cancellationToken = default)
    {
        var success = await _systemRegisterService.UpdateWholeRegisteredSystem(updateSystem, systemId, cancellationToken);

        if (!success)
        {
            return BadRequest();
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }

    /// <summary>
    /// Retrieves a list of the predfined default rights for the Product type, if any
    /// </summary>
    /// <param name="systemId">The Id of the Product </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [HttpGet("system/{systemId}/rights")]
    public async Task<ActionResult<List<Right>>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellationToken = default)
    {
        List<Right> lista = await _systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);
        if (lista is null || lista.Count == 0)
        {
            return NotFound();
        }

        return Ok(lista);
    }

    /// <summary>
    /// Inserts a new unique Maskinporten-ClientId, these are maintained by Maskinporten, and are inserted to our db by them.
    /// 
    /// </summary>
    /// <param name="clientId">The Client_Ids are maintained by Maskinporten, they are the key Maskinporten use
    /// to uniquely identify a SystemVendor's Registered Systems.</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [HttpPost("client/{clientId}")]
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> CreateClient(string clientId, CancellationToken cancellationToken = default)
    {
        var okay = await _systemRegisterService.CreateClient(clientId, cancellationToken);
        if (!okay)
        {
            return BadRequest();            
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }

    /// <summary>
    /// Create a new System 
    /// </summary>
    /// <param name="registerNewSystem">The descriptor model of a new Registered System</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [HttpPost("system")]    
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    public async Task<ActionResult<Guid>> CreateRegisteredSystem([FromBody] RegisterSystemRequest registerNewSystem, CancellationToken cancellationToken = default)
    {
        var registeredSystemGuid = await _systemRegisterService.CreateRegisteredSystem(registerNewSystem, cancellationToken);
        if (registeredSystemGuid is null)
        {
            return BadRequest();
        }

        return Ok(registeredSystemGuid);
    }

    /// <summary>
    /// Updates the rights on a registered system
    /// </summary>
    /// <param name="rights">A list of rights</param>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>true if changed</returns>
    [HttpPut("system/{systemId}/rights")]
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateRightsOnRegisteredSystem([FromBody] List<Right> rights, string systemId)
    {
        bool success = await _systemRegisterService.UpdateRightsForRegisteredSystem(rights, systemId);
        if (!success)
        {
            return BadRequest();
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }

    /// <summary>
    /// Set the registered system to be deleted.    
    /// </summary>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>true if changed</returns>
    [HttpDelete("system/{systemId}")]
    [Authorize(Policy = AuthzConstants.SCOPE_SYSTEMREGISTER_ADMIN)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> SetDeleteOnRegisteredSystem(string systemId)
    {
        bool deleted = await _systemRegisterService.SetDeleteRegisteredSystemById(systemId);
        if (!deleted) 
        { 
            return BadRequest(); 
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }
}