using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemUserService">The SystemUserService supports this API specifically.</param>
    /// <param name="generalSettings">The appsettings needed </param>
    public SystemUserController(
        ISystemUserService systemUserService, 
        IOptions<GeneralSettings> generalSettings)
    {
        _systemUserService = systemUserService;
        _generalSettings = generalSettings.Value;
    }

    /// <summary>
    /// Returns the list of SystemUsers this PartyID has registered
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = AuthzConstants.POLICY_ACCESS_MANAGEMENT_READ)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{party}")]
    public async Task<ActionResult> GetListOfSystemUsersPartyHas(int party)
    {
        List<SystemUser>? theList = await _systemUserService.GetListOfSystemUsersForParty(party);

        if (theList is not null && theList.Count > 0)
        {
            return Ok(theList);
        }

        return NotFound();
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
        if (systemUser is not null)
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
    // [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
    [HttpGet("internal/systemusers/stream", Name = "internal/systemusers/stream")]
    public async Task<ActionResult<Paginated<SystemUser>>> GetAllSystemUsers(
        [FromQuery(Name = "token")] Opaque<long>? token = null,
        CancellationToken cancellationToken = default)
    {        
        Result<Page<SystemUser, long>> pageResult = await _systemUserService.GetAllSystemUsers(
            token?.Value ?? 0,
            cancellationToken);
        if (pageResult.IsProblem)
        {
            return pageResult.Problem.ToActionResult();
        }

        var nextLink = pageResult.Value.ContinuationToken.HasValue
            ? Url.Link("internal/systemusers/stream", new
            {
                token = Opaque.Create(pageResult.Value.ContinuationToken.Value)
            })
            : null;

        if (pageResult.IsSuccess)
        {
            return Paginated.Create(pageResult.Value.Items.ToList(), nextLink);
        }

        return NotFound();
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
}    
