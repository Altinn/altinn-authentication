using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Authentication.Controllers;

/// <summary>
/// CRUD API for SystemRegister
/// </summary>
[Authorize]
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
    [Authorize(Policy = AuthnConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    public async Task<ActionResult> GetListOfRegisteredSystems(CancellationToken cancellationToken = default)
    {
        List<RegisteredSystem> lista = [];

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        return Ok(lista);
    }

    /// <summary>
    /// Retrieves a list of the predfined default rights for the Product type, if any
    /// </summary>
    /// <param name="productId">The Id of the Product </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns> 
    [Authorize(Policy = AuthnConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [HttpGet("product/{productId}")]
    public async Task<ActionResult> GetDefaultRightsForRegisteredSystem(string productId, CancellationToken cancellationToken = default)
    {
        List<DefaultRight> lista = await _systemRegisterService.GetDefaultRightsForRegisteredSystem(productId, cancellationToken);
        if (lista is null || lista.Count == 0) 
        {
            return NoContent();
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
    [Authorize(Policy = AuthnConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("client/{clientId}")]
    public async Task<ActionResult> CreateClient(string clientId, CancellationToken cancellationToken = default)
    {
        var okay = await _systemRegisterService.CreateClient(clientId, cancellationToken);
        if (!okay)
        {
            return BadRequest();            
        }

        return Ok(okay);
    }

    /// <summary>
    /// Create a new Registered System ( Product) 
    /// </summary>
    /// <param name="registerNewSystem">The descriptor model of a new Registered System</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [Authorize(Policy = AuthnConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [HttpPost("product")]
    public async Task<ActionResult> CreateRegisteredSystem([FromBody] RegisteredSystem registerNewSystem, CancellationToken cancellationToken = default)
    {
        var registeredSystemGuid = await _systemRegisterService.CreateRegisteredSystem(registerNewSystem, cancellationToken);
        if (registeredSystemGuid is null)
        {
            return BadRequest();
        }

        return Ok(registeredSystemGuid);
    }
}
