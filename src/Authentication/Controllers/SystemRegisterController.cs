using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Constants;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Errors;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Filters;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        List<RegisteredSystemResponse> lista = [];

        lista.AddRange(await _systemRegisterService.GetListRegSys(cancellationToken));

        List<RegisteredSystemDTO> registeredSystemDTOs = [];

        foreach (RegisteredSystemResponse system in lista)
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
        RegisteredSystemResponse registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);

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
    public async Task<ActionResult<RegisteredSystemResponse>> GetRegisteredSystemInfo(string systemId, CancellationToken cancellationToken = default)
    {
        RegisteredSystemResponse registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);

        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registeredSystem?.Vendor.ID), User))
        {
            return Forbid();
        }

        return Ok(registeredSystem);
    }

    /// <summary>
    /// Replaces the entire registered system
    /// </summary>
    /// <param name="proposedUpdateToSystem">The updated system model</param>
    /// <param name="systemId">The Id of the Registered System </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpPut("vendor/{systemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ServiceFilter(typeof(TrimStringsActionFilter))]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateWholeRegisteredSystem([FromBody] RegisterSystemRequest proposedUpdateToSystem, string systemId, CancellationToken cancellationToken = default)
    {
        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(proposedUpdateToSystem.Vendor.ID), User))
        {
            return Forbid();
        }

        if (proposedUpdateToSystem.Id != systemId)
        {
            ValidationErrorBuilder errors = default;
            errors.Add(ValidationErrors.SystemId_Mismatch, [ErrorPathConstant.SYSTEM_ID]);
            if (errors.TryToActionResult(out var result))
            {
                return result;
            }
        }

        RegisteredSystemResponse currentSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);

        if (currentSystem == null)
        {
            return NotFound($"System with ID '{systemId}' not found.");
        }

        List<string> allClientIds = CombineClientIds(currentSystem.ClientId, proposedUpdateToSystem.ClientId);
        List<MaskinPortenClientInfo> allClientIdUsages = await _systemRegisterService.GetMaskinportenClients(allClientIds, cancellationToken);

        ValidationErrorBuilder validateErrorRights = await ValidateRights(proposedUpdateToSystem.Rights, cancellationToken);
        ValidationErrorBuilder validateErrorAccessPackages = await ValidateAccessPackages(proposedUpdateToSystem.AccessPackages, cancellationToken);
        ValidationErrorBuilder validateErrorClientIds = await ValidateClientIds(currentSystem, proposedUpdateToSystem, allClientIdUsages);
        ValidationErrorBuilder mergedErrors = MergeValidationErrors(validateErrorRights, validateErrorAccessPackages, validateErrorClientIds);

        if (mergedErrors.TryToActionResult(out ActionResult errorResult))
        {
            return errorResult;
        }

        bool success = await _systemRegisterService.UpdateWholeRegisteredSystem(proposedUpdateToSystem, cancellationToken);

        if (!success)
        {
            return BadRequest("Unable to update system");
        }

        return Ok(new SystemRegisterUpdateResult(true));
    }

    private static List<string> CombineClientIds(IEnumerable<string> current, IEnumerable<string> updated) =>
        current.Union(updated, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

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

    /// <summary>
    /// Retrieves a list of the predfined default rights for the Product type, if any
    /// </summary>
    /// <param name="systemId">The Id of the Product </param>
    /// <param name="useOldFormatForApp">The old format for the App</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    [HttpGet("{systemId}/accesspackages")]
    public async Task<ActionResult<List<AccessPackage>>> GetAccessPackagesForRegisteredSystem(string systemId, [FromQuery] bool useOldFormatForApp = false, CancellationToken cancellationToken = default)
    {
        List<AccessPackage> lista = await _systemRegisterService.GetAccessPackagesForRegisteredSystem(systemId, cancellationToken);

        return Ok(lista);
    }

    /// <summary>
    /// Create a new System 
    /// </summary>
    /// <param name="registerNewSystem">The descriptor model of a new Registered System</param>
    /// <param name="cancellationToken">The Cancellationtoken</param>
    /// <returns></returns>
    [HttpPost("vendor")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [ServiceFilter(typeof(TrimStringsActionFilter))]
    public async Task<ActionResult<Guid>> CreateRegisteredSystem([FromBody] RegisterSystemRequest registerNewSystem, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidationErrorBuilder errors = default;
            if (!AuthenticationHelper.IsValidOrgIdentifier(registerNewSystem.Vendor.ID))
            {
                errors.Add(ValidationErrors.SystemRegister_InValid_Org_Identifier, [
                    ErrorPathConstant.VENDOR_ID
                ]);

                if (errors.TryToActionResult(out var orgIdentifierErrorResult))
                {
                    return orgIdentifierErrorResult;
                }
            }

            ValidationErrorBuilder validationErrorRegisteredSystem = await ValidateRegisteredSystem(registerNewSystem, cancellationToken);
            ValidationErrorBuilder validationErrorRights = await ValidateRights(registerNewSystem.Rights, cancellationToken);
            ValidationErrorBuilder validationErrorAccessPackages = await ValidateAccessPackages(registerNewSystem.AccessPackages, cancellationToken);

            errors = MergeValidationErrors(validationErrorRegisteredSystem, validationErrorRights, validationErrorAccessPackages);

            if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerNewSystem.Vendor.ID), User))
            {
                return Forbid();
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
    /// <param name="cancellationToken">a cancellation token</param>
    /// <returns>true if changed</returns>
    [HttpPut("vendor/{systemId}/rights")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateRightsOnRegisteredSystem([FromBody] List<Right> rights, string systemId, CancellationToken cancellationToken = default)
    {
        ValidationErrorBuilder errors = default;
        RegisteredSystemResponse registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerSystemResponse.Vendor.ID), User))
        {
            return Forbid();
        }

        errors = await ValidateRights(rights, cancellationToken);

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
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>true if changed</returns>
    [HttpPut("vendor/{systemId}/accesspackages")]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    public async Task<ActionResult<SystemRegisterUpdateResult>> UpdateAccessPackagesOnRegisteredSystem([FromBody] List<AccessPackage> accessPackages, string systemId, CancellationToken cancellationToken = default)
    {
        RegisteredSystemResponse registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
        if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registerSystemResponse.Vendor.ID), User))
        {
            return Forbid();
        }

        ValidationErrorBuilder errors = await ValidateAccessPackages(accessPackages, cancellationToken);

        if (errors.TryToActionResult(out var errorResult))
        {
            return errorResult;
        }

        bool success = await _systemRegisterService.UpdateAccessPackagesForRegisteredSystem(accessPackages, systemId, cancellationToken);
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
        RegisteredSystemResponse registerSystemResponse = await _systemRegisterService.GetRegisteredSystemInfo(systemId);

        if (registerSystemResponse == null)
        {
            ModelState.AddModelError("return", "System not found");
            return ValidationProblem(ModelState);
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

    private async Task<ValidationErrorBuilder> ValidateRights(List<Right> rights, CancellationToken cancellationToken)
    {
        ValidationErrorBuilder errors = default;
        if (!AuthenticationHelper.IsResourceIdFormatValid(rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_InvalidFormat, [
                ErrorPathConstant.RESOURCERIGHTS
            ]);
        }

        if (!await _systemRegisterService.DoesResourceIdExists(rights, cancellationToken))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_DoesNotExist, [
                ErrorPathConstant.RESOURCERIGHTS
            ]);
        }

        if (AuthenticationHelper.HasDuplicateRights(rights))
        {
            errors.Add(ValidationErrors.SystemRegister_ResourceId_Duplicates, [
                ErrorPathConstant.RESOURCERIGHTS
            ]);
        }

        return errors;
    }

    private async Task<ValidationErrorBuilder> ValidateAccessPackages(List<AccessPackage> accessPackages, CancellationToken cancellationToken)
    {
        ValidationErrorBuilder errors = default;

        var (invalidFormatUrns, notFoundUrns, notDelegableUrns) = await _systemRegisterService.GetInvalidAccessPackageUrnsDetailed(accessPackages, cancellationToken);
        if (invalidFormatUrns.Count > 0 || notFoundUrns.Count > 0 || notDelegableUrns.Count > 0)
        {
            var allInvalidUrns = new List<string>();
            if (invalidFormatUrns.Count > 0)
            {
                allInvalidUrns.Add($"Invalid format: {string.Join(", ", invalidFormatUrns)}");
            }

            if (notFoundUrns.Count > 0)
            {
                allInvalidUrns.Add($"Not found: {string.Join(", ", notFoundUrns)}");
            }

            if (notDelegableUrns.Count > 0)
            {
                allInvalidUrns.Add($"Not delegable: {string.Join(", ", notDelegableUrns)}");
            }

            var problemExtensionData = ProblemExtensionData.Create(new[]
            {
                new KeyValuePair<string, string>("Invalid Urn Details : ", string.Join(" | ", allInvalidUrns))
            });

            errors.Add(ValidationErrors.SystemRegister_AccessPackage_NotValid, ErrorPathConstant.ACCESSPACKAGES, problemExtensionData);
        }

        if (AuthenticationHelper.HasDuplicateAccessPackage(accessPackages))
        {
            errors.Add(ValidationErrors.SystemRegister_AccessPackage_Duplicates, [
                ErrorPathConstant.ACCESSPACKAGES
            ]);
        }

        return errors;
    }

    private static Task<ValidationErrorBuilder> ValidateClientIds(RegisteredSystemResponse currentSystem, RegisterSystemRequest proposedUpdateToSystem, List<MaskinPortenClientInfo> allClientUsages)
    {
        ValidationErrorBuilder errors = default;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        bool hasDuplicates = proposedUpdateToSystem.ClientId.Any(clientId => !seen.Add(clientId));

        if (hasDuplicates)
        {
            errors.Add(ValidationErrors.SystemRegister_Duplicate_ClientIds, [ErrorPathConstant.CLIENT_ID]);
        }

        // Check for client IDs used by other systems (not deleted and not ours)
        foreach (string clientId in proposedUpdateToSystem.ClientId)
        {
            bool usedByOther = allClientUsages.Any(usage =>
            {
                bool sameClientId = string.Equals(usage.ClientId, clientId, StringComparison.OrdinalIgnoreCase);
                bool differentSystem = usage.SystemInternalId != currentSystem.InternalId;
                bool isActive = !usage.IsDeleted;

                return sameClientId && differentSystem && isActive;
            });

            if (usedByOther)
            {
                errors.Add(ValidationErrors.SystemRegister_ClientID_Exists, [ErrorPathConstant.CLIENT_ID]);
            }
        }

        return Task.FromResult(errors);
    }

    private async Task<ValidationErrorBuilder> ValidateRegisteredSystem(RegisterSystemRequest systemToValidate, CancellationToken cancellationToken)
    {
        ValidationErrorBuilder errors = default;

        if (!AuthenticationHelper.DoesSystemIdStartWithOrgnumber(systemToValidate.Vendor.ID, systemToValidate.Id))
        {
            errors.Add(ValidationErrors.SystemRegister_Invalid_SystemId_Format, [
                ErrorPathConstant.SYSTEM_ID
            ]);
        }

        if (await _systemRegisterService.GetRegisteredSystemInfo(systemToValidate.Id, cancellationToken) != null)
        {
            errors.Add(ValidationErrors.SystemRegister_SystemId_Exists, [
                ErrorPathConstant.SYSTEM_ID
            ]);
        }

        if (!AuthenticationHelper.IsValidRedirectUrl(systemToValidate.AllowedRedirectUrls))
        {
            errors.Add(ValidationErrors.SystemRegister_InValid_RedirectUrlFormat, [
                ErrorPathConstant.ALLOWEDREDIRECT_URLS
            ]);
        }

        if (await _systemRegisterService.DoesClientIdExists(systemToValidate.ClientId, cancellationToken))
        {
            errors.Add(ValidationErrors.SystemRegister_ClientID_Exists, [
                ErrorPathConstant.CLIENT_ID
            ]);
        }

        return errors;
    }

    private static ValidationErrorBuilder MergeValidationErrors(params ValidationErrorBuilder[] errorBuilders)
    {
        ValidationErrorBuilder mergedErrors = default;
        foreach (var errorBuilder in errorBuilders)
        {
            foreach (var error in errorBuilder)
            {
                mergedErrors.Add(error);
            }
        }

        return mergedErrors;
    }
}