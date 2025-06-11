using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Altinn.Platform.Authentication.Controllers;

#nullable enable
/// <summary>
/// CRUD API for the System User 
/// </summary>
[FeatureGate(FeatureFlags.SystemUser)]
[Route("authentication/api/v1/systemuser")]
[ApiController]
public class SystemUserController : ControllerBase
{
    private readonly ISystemUserService _systemUserService;
    private readonly GeneralSettings _generalSettings;
    private readonly IRequestSystemUser _requestSystemUser;
    private readonly IFeatureManager _featureManager;

    /// <summary>
    /// Route name for the internal stream of systemusers used by the Registry
    /// </summary>
    public const string ROUTE_GET_STREAM = "internal/systemusers/stream";

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemUserService">The SystemUserService supports this API specifically.</param>
    /// <param name="requestSystemUser">The RequestUserService is called too</param>
    /// <param name="generalSettings">The appsettings needed </param>
    public SystemUserController(
        ISystemUserService systemUserService, 
        IRequestSystemUser requestSystemUser, 
        IOptions<GeneralSettings> generalSettings,
        IFeatureManager featureManager)
    {
        _systemUserService = systemUserService;
        _generalSettings = generalSettings.Value;
        _requestSystemUser = requestSystemUser;
        _featureManager = featureManager;
    }

    /// <summary>
    /// Returns the list of Default SystemUsers this PartyID has registered.
    /// No Agent SystemUsers are returned, use the other endpoint for them.
    /// </summary>
    /// <returns>List of SystemUsers</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("{party}")]
    public async Task<ActionResult<List<SystemUser>>> GetListOfSystemUsersPartyHas(int party)
    {
        var result = await _systemUserService.GetListOfSystemUsersForParty(party) ?? [];
        return Ok(result);
    }

    /// <summary>
    /// Returns the list of SystemUsers this PartyID has registered
    /// </summary>
    /// <returns>List of SystemUsers</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("agent/{party}")]
    public async Task<ActionResult<List<SystemUser>>> GetListOfAgentSystemUsersPartyHas(int party)
    {
        var result = await _systemUserService.GetListOfAgentSystemUsersForParty(party) ?? [];
        return Ok(result);
    }

    /// <summary>
    /// Get list of delegations to this agent systemuser
    /// </summary>
    /// <returns>List of DelegationResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("agent/{party}/{facilitator}/{systemUserId}/delegations")]
    public async Task<ActionResult<List<DelegationResponse>>> GetListOfDelegationsForAgentSystemUser(int party, Guid facilitator, Guid systemUserId)
    {
        List<DelegationResponse> ret = [];
        var result = await _systemUserService.GetListOfDelegationsForAgentSystemUser(party, facilitator, systemUserId);
        if (result.IsSuccess) 
        {
            ret = result.Value;
        }

        return Ok(ret);
    }

    /// <summary>
    /// Return a single SystemUser by PartyId and SystemUserId
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{party}/{systemUserId}")]
    public async Task<ActionResult> GetSingleSystemUserById(int party, Guid systemUserId)
    {
        SystemUser? systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (systemUser is not null && systemUser.PartyId == party.ToString())
        {
            return Ok(systemUser);
        }

        return NotFound();
    }

    /// <summary>
    /// Used by MaskinPorten, to find if a given systemOrg owns a SystemUser Integration for a Vendor's Product, by an ExternalId
    /// </summary>
    /// <param name="clientId">The unique id maintained by MaskinPorten tying their clients to the Registered Systems the ServiceProivders have created in our db.</param>        
    /// <param name="systemProviderOrgNo">The legal number (Orgno) of the Vendor creating the Registered System (Accounting system)</param>
    /// <param name="systemUserOwnerOrgNo">The legal number (Orgno) of the party owning the System User Integration. (The ReporteeOrgno)</param>
    /// <param name="externalRef">The External Reference is provided by the Vendor, and is used to identify their Customer in the Vendor's system.</param>
    /// <param name="cancellationToken">Cancellationtoken</param>/// 
    /// <returns>The SystemUserIntegration model API DTO</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERLOOKUP)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("byExternalId")]
    public async Task<ActionResult> CheckIfPartyHasIntegration(
        [FromQuery] string clientId, 
        [FromQuery] string systemProviderOrgNo, 
        [FromQuery] string systemUserOwnerOrgNo,
        [FromQuery] string? externalRef = null,
        CancellationToken cancellationToken = default)
    {
        // We dont't throw a badrequest for a missing externalRef yet, rather we set it equal to the orgno
        if (string.IsNullOrEmpty(clientId) 
            || string.IsNullOrEmpty(systemProviderOrgNo) 
            || string.IsNullOrEmpty(systemUserOwnerOrgNo))
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(externalRef))
        {
            externalRef = systemUserOwnerOrgNo;
        }

        SystemUser? res = await _systemUserService.CheckIfPartyHasIntegration(
            clientId, 
            systemProviderOrgNo, 
            systemUserOwnerOrgNo, 
            externalRef, 
            cancellationToken);

        if (res is null)
        {
            return NotFound();
        }

        // Temporary fix until Maskinporten changes their integration
        res.ProductName = res.SystemId;

        return Ok(res);
    }

    /// <summary>
    /// Set the Delete flag on the identified SystemUser
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{party}/{systemUserId}")]
    public async Task<ActionResult> SetDeleteFlagOnSystemUser(string party, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        SystemUser? toBeDeleted = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (toBeDeleted is not null)
        {
            await _systemUserService.SetDeleteFlagOnSystemUser(party, systemUserId, cancellationToken);
            await DeleteRequestForSystemUser(toBeDeleted);
            return Accepted(1);
        }

        return NotFound(0);            
    }

    private async Task DeleteRequestForSystemUser(SystemUser toBeDeleted)
    {
        ExternalRequestId ext = new(toBeDeleted.ReporteeOrgNo, toBeDeleted.ExternalRef, toBeDeleted.SystemId);
        var req = await _requestSystemUser.GetRequestByExternalRef(ext, OrganisationNumber.CreateFromStringOrgNo(toBeDeleted.SupplierOrgNo));
        if (req.IsSuccess)
        {
            await _requestSystemUser.DeleteRequestByRequestId(req.Value.Id);
        }
    }

    /// <summary>
    /// Replaces the values for the existing system user with those from the update. 
    /// </summary>
    /// <param name="request">The DTO describing the updateed SystemUser.</param>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut]
    public async Task<ActionResult> UpdateSystemUserById([FromBody] SystemUserUpdateDto request)
    {
        SystemUser? toBeUpdated = await _systemUserService.GetSingleSystemUserById(Guid.Parse(request.Id));
        if (toBeUpdated is not null)
        {
            // Need to verify that the partyId is the same as the one in the request
            // await _systemUserService.UpdateSystemUserById(request);
            return Ok();
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves a list of SystemUsers the Vendor has for a given system they own.
    /// </summary>
    /// <param name="systemId">The system the Vendor wants the list for</param>
    /// <param name="token">Optional continuation token</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("vendor/bysystem/{systemId}", Name = "vendor/systemusers/bysystem")]
    public async Task<ActionResult<Paginated<SystemUser>>> GetAllSystemUsersByVendorSystem(
        string systemId,
        [FromQuery(Name = "token")] Opaque<string>? token = null,
        CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Page<string>.Request continueFrom = null!;
        if (token?.Value is not null)
        {
            continueFrom = Page.ContinueFrom(token!.Value);
        }

        Result<Page<SystemUser, string>> pageResult = await _systemUserService.GetAllSystemUsersByVendorSystem(
            vendorOrgNo, systemId, continueFrom, cancellationToken);
        if (pageResult.IsProblem)
        {
            return pageResult.Problem.ToActionResult();
        }

        var nextLink = pageResult.Value.ContinuationToken.HasValue
            ? Url.Link("vendor/systemusers/bysystem", new
            {
                systemId,
                token = Opaque.Create(pageResult.Value.ContinuationToken.Value)
            })
            : null;

        if (pageResult.IsSuccess)
        {
            return Paginated.Create(pageResult.Value.Items.ToList(), nextLink);
        }

        return NotFound();
    }

    /// <summary>
    /// Retrieves a list of all SystemUsers for internal use, 
    /// called by the Register
    /// </summary>
    /// <param name="token">Optional continuation token</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Paginated list of all SystmUsers e</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_INTERNAL_OR_PLATFORM_ACCESS)]
    [HttpGet("internal/systemusers/stream", Name = ROUTE_GET_STREAM)]
    public async Task<ActionResult<ItemStream<SystemUserRegisterDTO>>> GetAllSystemUsers(
        [FromQuery(Name = "token")] Opaque<long>? token = null,
        CancellationToken cancellationToken = default)
    {
        Result<IEnumerable<SystemUserRegisterDTO>> pageResult = await _systemUserService.GetAllSystemUsers(
            token?.Value ?? 0,
            cancellationToken);
        if (pageResult.IsProblem)
        {
            return pageResult.Problem.ToActionResult();
        }

        List<SystemUserRegisterDTO> systemUserList = pageResult.Value.ToList();
        long maxSeq = await _systemUserService.GetMaxSystemUserSequenceNo();
        string? nextLink = null;

        if (systemUserList.Count > 0)
        {
            nextLink = Url.Link(ROUTE_GET_STREAM, new
            {
                token = Opaque.Create(systemUserList[^1].SequenceNo)                
            });    
        }        

        return ItemStream.Create(
            pageResult.Value,
            next: nextLink,
            sequenceMax: maxSeq,
            sequenceNumberFactory: static s => s.SequenceNo);            
    }

    private OrganisationNumber? RetrieveOrgNoFromToken()
    {
        string token = JwtTokenUtil.GetTokenFromContext(HttpContext, _generalSettings.JwtCookieName);
        JwtSecurityToken jwtSecurityToken = new(token);
        foreach (Claim claim in jwtSecurityToken.Claims)
        {
            // ID-porten specific claims
            if (claim.Type.Equals("consumer"))
            {
                return OrganisationNumber.CreateFromMaskinPortenToken(claim.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new SystemUser.
    /// </summary>
    /// <returns>SystemUser response model</returns>    
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SystemUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPost("{party}/create")]
    public async Task<ActionResult<SystemUser>> CreateAndDelegateSystemUser(string party, [FromBody] SystemUserRequestDto request, CancellationToken cancellationToken)
    {
        var userId = AuthenticationHelper.GetUserId(HttpContext);

        Result<SystemUser> createdSystemUser = await _systemUserService.CreateAndDelegateSystemUser(party, request, userId, cancellationToken);
        if (createdSystemUser.IsSuccess)
        {
            return Ok(createdSystemUser.Value);
        }

        return createdSystemUser.Problem.ToActionResult();
    }

    /// <summary>
    /// Creates a new delegation of a customer to an Agent SystemUser.
    /// The endpoint is idempotent.
    /// </summary>
    /// <returns>OK</returns>    
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPost("agent/{party}/{systemUserId}/delegation/")]
    public async Task<ActionResult<List<DelegationResponse>>> DelegateToAgentSystemUser(string party, Guid systemUserId, [FromBody] AgentDelegationInputDto request, CancellationToken cancellationToken)
    {
        var userId = AuthenticationHelper.GetUserId(HttpContext);

        SystemUser? systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (systemUser is null)
        {
            ModelState.AddModelError("return", $"SystemUser with Id {systemUserId} Not Found");
            return ValidationProblem(ModelState);
        }

        if (systemUser.PartyId != party)
        {
            return Forbid();
        }

        Result<List<DelegationResponse>> delegationResult = await _systemUserService.DelegateToAgentSystemUser(systemUser, request, userId, _featureManager, cancellationToken);
        if (delegationResult.IsSuccess)
        {
            return Ok(delegationResult.Value);
        }

        return delegationResult.Problem.ToActionResult();
    }

    /// <summary>
    /// Delete a customer from an Agent SystemUser.
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("agent/{party}/delegation/{delegationId}")]
    public async Task<ActionResult> DeleteCustomerFromAgentSystemUser(string party, Guid delegationId, [FromQuery]Guid facilitatorId, CancellationToken cancellationToken = default)
    {
        Result<bool> result = await _systemUserService.DeleteClientDelegationToAgentSystemUser(party, delegationId, facilitatorId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok();
        }

        return result.Problem.ToActionResult();
    }

    /// <summary>
    /// Delete an Agent SystemUser.
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("agent/{party}/{systemUserId}")]
    public async Task<ActionResult> DeleteAgentSystemUser(string party, Guid systemUserId, [FromQuery]Guid facilitatorId, CancellationToken cancellationToken = default)
    {
        Result<bool> result = await _systemUserService.DeleteAgentSystemUser(party, systemUserId, facilitatorId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok();
        }

        return result.Problem.ToActionResult();
    }

    /// <summary>
    /// Get list of clients for a facilitator
    /// </summary>
    /// <returns>List of Clients</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("agent/{party}/clients")]
    public async Task<ActionResult<List<Customer>>> GetClientsForFacilitator([FromQuery]Guid facilitator, [FromQuery] List<string> packages = null, CancellationToken cancellationToken = default)
    {
        List<Customer> ret = [];
        var result = await _systemUserService.GetClientsForFacilitator(facilitator, packages, _featureManager, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Problem.ToActionResult();
    }
}    
