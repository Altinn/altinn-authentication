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
using Altinn.Register.Contracts.V1;
using AltinnCore.Authentication.Utils;
using AutoMapper;
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
    private readonly IMapper _mapper;

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
        IFeatureManager featureManager,
        IMapper mapper)
    {
        _systemUserService = systemUserService;
        _generalSettings = generalSettings.Value;
        _requestSystemUser = requestSystemUser;
        _featureManager = featureManager;
        _mapper = mapper;
    }

    /// <summary>
    /// Returns the list of Default SystemUsers this PartyID has registered.
    /// No Agent SystemUsers are returned, use the other endpoint for them.
    /// </summary>
    /// <returns>List of SystemUsers</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("{party}")]
    public async Task<ActionResult<List<SystemUserInternalDTO>>> GetListOfSystemUsersPartyHas(int party)
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
    public async Task<ActionResult<List<SystemUserInternalDTO>>> GetListOfAgentSystemUsersPartyHas(int party)
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
    public async Task<ActionResult> GetSingleSystemUserById(int party, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        SystemUserInternalDTO? systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (systemUser is not null && systemUser.PartyId == party.ToString())
        {
            SystemUserDetailInternalDTO systemUserDetailDTO = await PopulateSystemUserDetail(party, systemUserId, systemUser, cancellationToken);
            return Ok(systemUserDetailDTO);
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

        SystemUserInternalDTO? res = await _systemUserService.CheckIfPartyHasIntegration(
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
        SystemUserExternalDTO systemUserExternal = _mapper.Map<SystemUserExternalDTO>(res);
        return Ok(systemUserExternal);
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
        SystemUserInternalDTO? toBeDeleted = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (toBeDeleted is not null)
        {
            var deleteResult = await _systemUserService.SetDeleteFlagOnSystemUser(party, systemUserId, cancellationToken);
            if (deleteResult.IsProblem)
            {
                return deleteResult.Problem.ToActionResult();
            }

            await DeleteRequestForSystemUser(toBeDeleted);
            return Accepted(1);
        }

        return NotFound(0);            
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
        SystemUserInternalDTO? toBeUpdated = await _systemUserService.GetSingleSystemUserById(Guid.Parse(request.Id));
        if (toBeUpdated is not null)
        {
            // Need to verify that the partyId is the same as the one in the request
            // await _systemUserService.UpdateSystemUserById(request);
            return Ok();
        }

        return NotFound();
    }

    /// <summary>
    /// An endpoint where the Vendor can retrieve a SystemUser
    /// by the organisation number, system-id and optionally the external-ref
    /// </summary>
    /// <param name="systemId">Required: the id the vendor system used</param>
    /// <param name="externalRef">Optional: a disambiguation string</param>
    /// <param name="orgno">Required: the organisation number for the Reportee (owner of the SystemUser)</param>
    /// <param name="cancellationToken">the cancellationtoken</param>
    /// <returns>The SystemUser model</returns>
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE)]
    [HttpGet("vendor/byquery", Name = "vendor/byquery")]
    public async Task<ActionResult<SystemUserExternalDTO>> GetSingleSystemUserForVendor(
        [FromQuery(Name = "system-id")] string systemId,
        [FromQuery(Name = "external-ref")] string? externalRef,
        [FromQuery(Name = "orgno")] string orgno,
        CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty()) 
        {
            return Unauthorized();
        }

        ExternalRequestId extid = new()
        {
            ExternalRef = externalRef ?? orgno,  
            OrgNo = orgno,
            SystemId = systemId            
        };  

        SystemUserInternalDTO? toBeFound = await _systemUserService.GetSystemUserByExternalRequestId(extid, cancellationToken);

        if (toBeFound is not null && OrganisationNumber.CreateFromStringOrgNo(toBeFound.SupplierOrgNo) == vendorOrgNo)
        {
            SystemUserExternalDTO systemUserExternalDTO = _mapper.Map<SystemUserExternalDTO>(toBeFound);
            return Ok(systemUserExternalDTO);            
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
    public async Task<ActionResult<Paginated<SystemUserExternalDTO>>> GetAllSystemUsersByVendorSystem(
        string systemId,
        [FromQuery(Name = "token")] Opaque<long>? token = null,
        CancellationToken cancellationToken = default)
    {
        OrganisationNumber? vendorOrgNo = RetrieveOrgNoFromToken();
        if (vendorOrgNo is null || vendorOrgNo == OrganisationNumber.Empty())
        {
            return Unauthorized();
        }

        Page<long>.Request continueFrom = null!;
        if (token?.Value is not null)
        {
            continueFrom = Page.ContinueFrom(token!.Value);
        }

        Result<Page<SystemUserInternalDTO, long>> pageResult = await _systemUserService.GetAllSystemUsersByVendorSystem(
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
            // Use AutoMapper to map the list
            var externalList = _mapper.Map<List<SystemUserExternalDTO>>(pageResult.Value.Items.ToList());
            return Paginated.Create(externalList, nextLink);
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

    /// <summary>
    /// Creates a new SystemUser.
    /// </summary>
    /// <returns>SystemUser response model</returns>    
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_WRITE)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SystemUserInternalDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPost("{party}/create")]
    public async Task<ActionResult<SystemUserInternalDTO>> CreateAndDelegateSystemUser(string party, [FromBody] SystemUserRequestDto request, CancellationToken cancellationToken)
    {
        var userId = AuthenticationHelper.GetUserId(HttpContext);

        Result<SystemUserInternalDTO> createdSystemUser = await _systemUserService.CreateAndDelegateSystemUser(party, request, userId, cancellationToken);
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

        SystemUserInternalDTO? systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
        if (systemUser is null)
        {
            ModelState.AddModelError("return", $"SystemUser with Id {systemUserId} Not Found");
            return ValidationProblem(ModelState);
        }

        if (systemUser.PartyId != party)
        {
            return Forbid();
        }

        Result<List<DelegationResponse>> delegationResult = await _systemUserService.DelegateToAgentSystemUser(systemUser, request, userId, cancellationToken);
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
        await DeleteRequestForSystemUser(systemUserId);
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
        var result = await _systemUserService.GetClientsForFacilitator(facilitator, packages, _featureManager, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Problem.ToActionResult();
    }

    /// <summary>
    /// Get list of delegations for a standard systemuser
    /// </summary>
    /// <returns>List of DelegationResponse</returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("{party}/{systemUserId}/delegations")]
    public async Task<ActionResult<StandardSystemUserDelegations>> GetListOfDelegationsForStandardSystemUser(int party, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        StandardSystemUserDelegations delegations = new StandardSystemUserDelegations();
        var result = await _systemUserService.GetListOfDelegationsForStandardSystemUser(party, systemUserId, cancellationToken);
        if (result.IsProblem)
        {
            return result.Problem.ToActionResult();
        }

        return Ok(result.Value);
    }

    private async Task DeleteRequestForSystemUser(SystemUserInternalDTO toBeDeleted)
    {
        ExternalRequestId ext = new(toBeDeleted.ReporteeOrgNo, toBeDeleted.ExternalRef, toBeDeleted.SystemId);
        var req = await _requestSystemUser.GetRequestByExternalRef(ext, OrganisationNumber.CreateFromStringOrgNo(toBeDeleted.SupplierOrgNo));
        if (req.IsSuccess)
        {
            await _requestSystemUser.DeleteRequestByRequestId(req.Value.Id);
        }
    }

    private async Task DeleteRequestForSystemUser(Guid toBeDeleted)
    {
        SystemUserInternalDTO? systemUser = await _systemUserService.GetSingleSystemUserById(toBeDeleted);
        if (systemUser == null)
        {
            return;
        }

        ExternalRequestId ext = new(systemUser.ReporteeOrgNo, systemUser.ExternalRef, systemUser.SystemId);
        var req = await _requestSystemUser.GetAgentRequestByExternalRef(ext, OrganisationNumber.CreateFromStringOrgNo(systemUser.SupplierOrgNo));
        if (req.IsSuccess)
        {
            await _requestSystemUser.DeleteRequestByRequestId(req.Value.Id);
        }
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

    private async Task<SystemUserDetailInternalDTO> PopulateSystemUserDetail(int party, Guid systemUserId, SystemUserInternalDTO systemUser, CancellationToken cancellationToken)
    {
        SystemUserDetailInternalDTO systemUserDetailDTO = new SystemUserDetailInternalDTO();
        systemUserDetailDTO = _mapper.Map<SystemUserDetailInternalDTO>(systemUser);

        if (systemUser.UserType == SystemUserType.Standard)
        {
            var restult = await _systemUserService.GetListOfDelegationsForStandardSystemUser(party, systemUserId, cancellationToken);
            if (restult.IsSuccess)
            {
                StandardSystemUserDelegations systemUserDelegations = restult.Value;
                systemUserDetailDTO.AccessPackages = systemUserDelegations.AccessPackages;
                systemUserDetailDTO.Rights = systemUserDelegations.Rights;
            }
        }

        return systemUserDetailDTO;
    }
}    
