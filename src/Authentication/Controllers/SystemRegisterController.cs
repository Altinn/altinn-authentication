using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Authentication.Controllers;

/// <summary>
/// CRUD API for SystemRegister
/// </summary>
[Route("authfront/api/v1/systemregister")]
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
    /// Retrieves the List of the Registered Systems
    /// </summary>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns></returns>
    //[Authorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet]
    public async Task<ActionResult> GetListOfRegisteredSystems(CancellationToken cancellationToken = default)
    {
        List<RegisteredSystem> lista = new();

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        return Ok(lista);
    }

    /// <summary>
    /// Retrieves a list of the predfined default rights for the Product type
    /// </summary>
    /// <param name="productId">The Id of the Product </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [HttpGet("product/{productId}")]
    public async Task<ActionResult> GetDefaultRightsForProductName(string productId, CancellationToken cancellationToken = default)
    {
        List<DefaultRights> lista = new();
        DefaultRights l1 = new() { Right = "Mva Registrering", ServiceProvider = "Skatteetaten" };
        DefaultRights l2 = new() { Right = "Lønns Rapportering", ServiceProvider = "Skatteetaten" };
        DefaultRights l3 = new() { Right = "Lakselus Rapportering", ServiceProvider = "Mattilsynet" };
        lista.Add(l1);
        lista.Add(l2);
        lista.Add(l3);
        return Ok(lista);
    }
}
