using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Errors;
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
    public async Task<ActionResult<List<RegisteredSystemDTO>>> GetListOfRegisteredSystems(CancellationToken cancellationToken = default)
    {
        List<RegisteredSystem> lista = [];

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        List<RegisteredSystemDTO> registeredSystemDTOs = [];

        foreach (RegisteredSystem system in lista)
        {
            registeredSystemDTOs.Add(AuthenticationHelper.MapRegisteredSystemToRegisteredSystemDTO(system));
        }

        return Ok(registeredSystemDTOs);
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
        RegisteredSystem registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
        
        if (registeredSystem == null)
        {
            return NotFound();
        }

        RegisteredSystemDTO registeredSystemDto = AuthenticationHelper.MapRegisteredSystemToRegisteredSystemDTO(registeredSystem);

        return Ok(registeredSystemDto);
    }

    /// <summary>
    /// Retrieves a Registered System for the systemId.
    /// </summary>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("vendor/{systemId}")]
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
    [HttpPut("vendor/{systemId}")]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateWholeRegisteredSystem([FromBody] RegisteredSystem updateSystem, string systemId, CancellationToken cancellationToken = default)
    {
        ValidationErrorBuilder errors = default;
        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(updateSystem.Vendor.ID), User))
        {
            return Forbid();
        }

        if (AuthenticationHelper.HasDuplicateRights(updateSystem.Rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_Duplicates, [
                "/registersystemrequest/rights/resource"
            ]);
        }

        List<MaskinPortenClientInfo> maskinPortenClients = await _systemRegisterService.GetMaskinportenClients(updateSystem.ClientId, cancellationToken);
        RegisteredSystem systemInfo = await _systemRegisterService.GetRegisteredSystemInfo(systemId);

        if (AuthenticationHelper.DoesResourceAlreadyExists(updateSystem.Rights, systemInfo.Rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_AlreadyExists, [
                "/registersystemrequest/rights/resource"
            ]);
        }

        foreach (string clientId in updateSystem.ClientId)
        {
            bool clientExistsForAnotherSystem = maskinPortenClients.FindAll(x => x.ClientId == clientId && x.SystemInternalId != systemInfo.InternalId).Count > 0;
            if (clientExistsForAnotherSystem)
            {
                ModelState.AddModelError("ClientId", $"ClientId {clientId} already tagged with another system");
                return BadRequest(ModelState);
            }
        }

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
    /// <param name="useOldFormatForApp">The old format for the App</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [HttpGet("{systemId}/rights")]
    public async Task<ActionResult<List<Right>>> GetRightsForRegisteredSystem(string systemId, [FromQuery] bool useOldFormatForApp = false, CancellationToken cancellationToken = default)
    {
        List<Right> lista = await _systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);

        if (lista is null || lista.Count == 0)
        {
            return NotFound();
        }
        else
        {
            if (useOldFormatForApp)
            {
                foreach (Right right in lista)
                {
                    var resource = new List<AttributePair>();
                    resource = DelegationHelper.ConvertAppResourceToOldResourceFormat(right.Resource);

                    right.Resource = resource;
                }

                return Ok(lista);
            }
            else
            {
                return Ok(lista);
            }
        }
    }

    /// <summary>
    /// Create a new System 
    /// </summary>
    /// <param name="registerNewSystem">The descriptor model of a new Registered System</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [HttpPost("vendor")]    
    ////[Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<Guid>> CreateRegisteredSystem([FromBody] RegisteredSystem registerNewSystem, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidationErrorBuilder errors = default;

            if (!AuthenticationHelper.IsValidOrgIdentifier(registerNewSystem.Vendor.ID))
            {
                errors.Add(ValidationErrors.SystemRegister_InValid_Org_Identifier, [
                    "/registersystemrequest/vendor/id"
                ]);

                if (errors.TryToActionResult(out var orgIdentifierErrorResult))
                {
                    return orgIdentifierErrorResult;
                }
            }

            if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerNewSystem.Vendor.ID), User))
            {
                return Forbid();
            }

            if (!AuthenticationHelper.DoesSystemIdStartWithOrgnumber(registerNewSystem))
            {
                errors.Add(ValidationErrors.SystemRegister_Invalid_SystemId_Format, [
                    "/registersystemrequest/systemid"
                ]);
            }

            if (await _systemRegisterService.GetRegisteredSystemInfo(registerNewSystem.Id, cancellationToken) != null)
            {
                errors.Add(ValidationErrors.SystemRegister_SystemId_Exists, [
                    "/registersystemrequest/systemid"
                ]);
            }

            if (!await _systemRegisterService.DoesResourceIdExists(registerNewSystem.Rights, cancellationToken))
            {
                errors.Add(ValidationErrors.SystemRegister_ResourceId_DoesNotExist, [
                    "/registersystemrequest/rights/resource"
                ]);
            }

            if (AuthenticationHelper.HasDuplicateRights(registerNewSystem.Rights))
            {
                errors.Add(ValidationErrors.SystemRegister_ResourceId_Duplicates, [
                    "/registersystemrequest/rights/resource"
                ]);
            }

            //// ToDO : integrate with AM api
            //if (await _systemRegisterService.HasValidAccessPackages(registerNewSystem.AccessPackages, cancellationToken))
            //{
            //    errors.Add(ValidationErrors.SystemRegister_AccessPackage_Duplicates, [
            //        "/registersystemrequest/accessPackages"
            //    ]);
            //}

            if (AuthenticationHelper.HasDuplicateAccessPackage(registerNewSystem.AccessPackages))
            {
                errors.Add(ValidationErrors.SystemRegister_AccessPackage_Duplicates, [
                    "/registersystemrequest/accesspackages"
                ]);
            }

            if (!AuthenticationHelper.IsValidRedirectUrl(registerNewSystem.AllowedRedirectUrls))
            {
                errors.Add(ValidationErrors.SystemRegister_InValid_RedirectUrlFormat, [
                    "/registersystemrequest/allowedredirecturls"
                ]);
            }

            if (await _systemRegisterService.DoesClientIdExists(registerNewSystem.ClientId, cancellationToken))
            {
                errors.Add(ValidationErrors.SystemRegister_ClientID_Exists, [
                    "/registersystemrequest/clientid"
                ]);
            }

            if (errors.TryToActionResult(out var errorResult))
            {
                return errorResult;
            }
            
            var registeredSystemGuid = await _systemRegisterService.CreateRegisteredSystem(registerNewSystem, cancellationToken);
            if (registeredSystemGuid is null)
            {
                return BadRequest();
            }

            return Ok(registeredSystemGuid);            
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
    [HttpPut("vendor/{systemId}/rights")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateRightsOnRegisteredSystem([FromBody] List<Right> rights, string systemId, CancellationToken cancellationToken = default)
    {
        ValidationErrorBuilder errors = default;
        RegisteredSystem registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (!AuthenticationHelper.HasWriteAccess(registerSystemResponse.SystemVendorOrgNumber, User))
        {
            return Forbid();
        }

        if (AuthenticationHelper.HasDuplicateRights(rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_Duplicates, [
                "/registersystemrequest/rights/resource"
            ]);
        }

        if (AuthenticationHelper.DoesResourceAlreadyExists(rights, registerSystemResponse.Rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_AlreadyExists, [
                "/registersystemrequest/rights/resource"
            ]);
        }

        if (!await _systemRegisterService.DoesResourceIdExists(rights, cancellationToken))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_DoesNotExist, [
                "/registersystemrequest/rights/resource"
            ]);
        }

        if (errors.TryToActionResult(out var errorResult))
        {
            return errorResult;
        }

        bool success = await _systemRegisterService.UpdateRightsForRegisteredSystem(rights, systemId);
        if (!success)
        {
            return BadRequest();
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }

    /// <summary>
    /// Updates the rights on a registered system
    /// </summary>
    /// <param name="accessPackages">A list of access packages</param>
    /// <param name="systemId">The human readable string id</param>
    /// <returns>true if changed</returns>
    [HttpPut("vendor/{systemId}/accesspackages")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateAccessPackagesOnRegisteredSystem([FromBody] List<AttributePair> accessPackages, string systemId, CancellationToken cancellationToken = default)
    {
        ValidationErrorBuilder errors = default;
        RegisteredSystem registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (!AuthenticationHelper.HasWriteAccess(registerSystemResponse.SystemVendorOrgNumber, User))
        {
            return Forbid();
        }

        if (AuthenticationHelper.HasDuplicateAccessPackage(accessPackages))
        {
            errors.Add(ValidationErrors.SystemRegister_AccessPackage_Duplicates, [
                "/registersystemrequest/accesspackages"
            ]);
        }

        //// ToDO : integrate with AM api
        //if (!await _systemRegisterService.HasValidAccessPackages(accessPackages, cancellationToken))
        //{
        //    errors.Add(ValidationErrors.SystemRegister_AccessPackage_DoesNotExist, [
        //        "/registersystemrequest/accesspackages"
        //    ]);
        //}

        if (errors.TryToActionResult(out var errorResult))
        {
            return errorResult;
        }

        bool success = await _systemRegisterService.UpdateAccessPackagesForRegisteredSystem(accessPackages, systemId);
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
    [HttpDelete("vendor/{systemId}")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> SetDeleteOnRegisteredSystem(string systemId)
    {
        RegisteredSystem registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);

        if(registerSystemResponse == null)
        {
            return BadRequest();
        }

        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerSystemResponse?.Vendor?.ID), User))
        {
            return Forbid();
        }

        bool deleted = await _systemRegisterService.SetDeleteRegisteredSystemById(systemId, registerSystemResponse.InternalId);
        if (!deleted) 
        { 
            return BadRequest();
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }
}