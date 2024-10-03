﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Azure.Identity;
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
    public async Task<ActionResult<List<RegisteredSystemDTO>>> GetListOfRegisteredSystems(CancellationToken cancellationToken = default)
    {
        List<RegisteredSystemDTO> lista = [];

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        return Ok(lista);
    }

    /// <summary>
    /// Retrieves a Registered System frontend DTO for the systemId.
    /// </summary>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [HttpGet("{systemId}")]
    public async Task<ActionResult<RegisteredSystemDTO>> GetRegisteredSystemDto(string systemId, CancellationToken cancellationToken = default)
    {
        Result<RegisteredSystemDTO> registeredSystem = await _systemRegisterService.GetRegisteredSystemDto(systemId, cancellationToken);

        if (registeredSystem.IsProblem) 
        { 
            return registeredSystem.Problem.ToActionResult();
        }

        return Ok(registeredSystem.Value);
    }

    /// <summary>
    /// Retrieves a full model Registered System for the systemId.
    /// Used by Vendors with scope
    /// </summary>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("system/{systemId}")]
    public async Task<ActionResult<RegisteredSystem>> GetRegisteredSystemInfo(string systemId, CancellationToken cancellationToken = default)
    {
        RegisteredSystem registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
        
        return Ok(registeredSystem);
    }

    /// <summary>
    /// Replaces the entire registered system
    /// </summary>
    /// <param name="updateSystem">The updated system model</param>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPut("system/{systemId}")]
    public async Task<ActionResult<RegisteredSystemUpdateResult>> UpdateWholeRegisteredSystem([FromBody] RegisterSystemRequest updateSystem, string systemId, CancellationToken cancellationToken = default)
    {
        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(updateSystem.Vendor.ID), User))
        {
            return Forbid();
        }

        List<MaskinPortenClientInfo> maskinPortenClients = await _systemRegisterService.GetMaskinportenClients(updateSystem.ClientId, cancellationToken);
        RegisteredSystem systemInfo = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        foreach (string clientId in updateSystem.ClientId)
        {
            bool clientExistsForAnotherSystem = maskinPortenClients.FindAll(x => x.ClientId == clientId && x.SystemInternalId != systemInfo.SystemInternalId).Count > 0;
            if (clientExistsForAnotherSystem)
            {
                ModelState.AddModelError("ClientId",$"ClientId {clientId} already tagged with another system");
                return BadRequest(ModelState);
            }
        }

        var success = await _systemRegisterService.UpdateWholeRegisteredSystem(updateSystem, systemId, cancellationToken);

        if (!success)
        {
            return BadRequest();
        }

        return Ok(new RegisteredSystemUpdateResult(true));
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
    /// Create a new System 
    /// </summary>
    /// <param name="registerNewSystem">The descriptor model of a new Registered System</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [HttpPost("system")]    
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<Guid>> CreateRegisteredSystem([FromBody] RegisterSystemRequest registerNewSystem, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!AuthenticationHelper.IsValidOrgIdentifier(registerNewSystem.Vendor.ID))
            {
                ModelState.AddModelError("Vendor", "the org number identifier is not valid ISO6523 identifier");
                return BadRequest(ModelState);
            }

            if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerNewSystem.Vendor.ID), User))
            {
                return Forbid();
            }

            if (!AuthenticationHelper.DoesSystemIdStartWithOrgnumber(registerNewSystem))
            {
                ModelState.AddModelError("SystemId", "The system id does not match the format orgnumber_xxxx...");
                return BadRequest(ModelState);
            }

            if (await _systemRegisterService.GetRegisteredSystemInfo(registerNewSystem.Id, cancellationToken) != null)
            {
                ModelState.AddModelError("SystemId", "The system id already exists");
                return BadRequest(ModelState);
            }

            if (await _systemRegisterService.DoesClientIdExists(registerNewSystem.ClientId, cancellationToken))
            {
                ModelState.AddModelError("ClientId", "One of the client id already tagged with an existing system");
                return BadRequest(ModelState);
            }
            else
            {
                var registeredSystemGuid = await _systemRegisterService.CreateRegisteredSystem(registerNewSystem, cancellationToken);
                if (registeredSystemGuid is null)
                {
                    return BadRequest();
                }

                return Ok(registeredSystemGuid);
            }
        }
        catch (Exception e)
        {
            return e.Message.Contains("duplicate key value violates unique constraint")
                ? Conflict($"The System already exist: {registerNewSystem.Id}")
                : StatusCode(500, e.Message);
        }
    }

    /// <summary>
    /// Updates the rights on a registered system
    /// </summary>
    /// <param name="rights">A list of rights</param>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>true if changed</returns>
    [HttpPut("system/{systemId}/rights")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<RegisteredSystemUpdateResult>> UpdateRightsOnRegisteredSystem([FromBody] List<Right> rights, string systemId)
    {
        RegisterSystemResponse registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (!AuthenticationHelper.HasWriteAccess(registerSystemResponse.SystemVendorOrgNumber, User))
        {
            return Forbid();
        }

        bool success = await _systemRegisterService.UpdateRightsForRegisteredSystem(rights, systemId);
        if (!success)
        {
            return BadRequest();
        }

        return Ok(new RegisteredSystemUpdateResult(true));
    }

    /// <summary>
    /// Set the registered system to be deleted.    
    /// </summary>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>true if changed</returns>
    [HttpDelete("system/{systemId}")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<RegisteredSystemUpdateResult>> SetDeleteOnRegisteredSystem(string systemId)
    {
        RegisterSystemResponse registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (!AuthenticationHelper.HasWriteAccess(registerSystemResponse?.SystemVendorOrgNumber, User))
        {
            return Forbid();
        }

        bool deleted = await _systemRegisterService.SetDeleteRegisteredSystemById(systemId);
        if (!deleted) 
        { 
            return BadRequest();
        }

        return Ok(new RegisteredSystemUpdateResult(true));
    }
}