using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authentication.Integration.Clients;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Authorization;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Authorization;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services;
#nullable enable

/// <inheritdoc/>
public class RequestSystemUserService(
    IHttpContextAccessor httpContextAccessor,
    ISystemRegisterService systemRegisterService,
    IPartiesClient partiesClient,
    IPDP pdp,
    ISystemRegisterRepository systemRegisterRepository,
    IAccessManagementClient accessManagementClient,
    IRequestRepository requestRepository,
    IOptions<PaginationOptions> _paginationOption,
    DelegationHelper delegationHelper)
    : IRequestSystemUser
{
    /// <summary>
    /// Used to limit the number of items returned in a paginated list
    /// </summary>
    private int _paginationSize = _paginationOption.Value.Size;
    
    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponse>> CreateRequest(CreateRequestSystemUser createRequest, OrganisationNumber vendorOrgNo)
    {
        // The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers.
        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createRequest.ExternalRef ?? createRequest.PartyOrgNo,
            OrgNo = createRequest.PartyOrgNo,
            SystemId = createRequest.SystemId,
        };

        RegisteredSystemResponse? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(createRequest.SystemId);
        if (systemInfo is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<bool> valRef = await ValidateExternalRequestId(externalRequestId);
        if (valRef.IsProblem)
        {
            return valRef.Problem;
        }

        Result<bool> valVendor = ValidateVendorOrgNo(vendorOrgNo, systemInfo);
        if (valVendor.IsProblem)
        {
            return valVendor.Problem;
        }

        Result<bool> valCust = await ValidateCustomerOrgNo(createRequest.PartyOrgNo);
        if (valCust.IsProblem)
        {
            return valCust.Problem;
        }

        if (createRequest.RedirectUrl is not null && createRequest.RedirectUrl != string.Empty)
        {
            var valRedirect = AuthenticationHelper.ValidateRedirectUrl(createRequest.RedirectUrl, systemInfo.AllowedRedirectUrls);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }

        // Must have a minimum of either one Right or one Accesspackage for a Standard SystemUser
        if (createRequest.Rights.Count == 0 && createRequest.AccessPackages.Count == 0)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        Result<bool> valRights = ValidateRights(createRequest.Rights, systemInfo);
        if (valRights.IsProblem)
        {
            return valRights.Problem;
        }

        Result<bool> valPackages = ValidateAccessPackages(createRequest.AccessPackages, systemInfo);
        if (valPackages.IsProblem)
        {
            return valPackages.Problem;
        }

        // Set an empty ExternalRef to be equal to the PartyOrgNo
        if (createRequest.ExternalRef is null || createRequest.ExternalRef == string.Empty)
        {
            createRequest.ExternalRef = createRequest.PartyOrgNo;
        }

        Guid newId = Guid.NewGuid();

        var created = new RequestSystemResponse()
        {
            Id = newId,
            ExternalRef = createRequest.ExternalRef,
            SystemId = createRequest.SystemId,
            PartyOrgNo = createRequest.PartyOrgNo,
            Rights = createRequest.Rights,
            AccessPackages = createRequest.AccessPackages,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createRequest.RedirectUrl
        };

        Result<bool> res = await requestRepository.CreateRequest(created);
        if (res.IsProblem)
        {
            return Problem.RequestCouldNotBeStored;
        }

        return created;
    }

    /// <inheritdoc/>
    public async Task<Result<AgentRequestSystemResponse>> CreateAgentRequest(CreateAgentRequestSystemUser createAgentRequest, OrganisationNumber vendorOrgNo)
    {
        // The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers.
        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createAgentRequest.ExternalRef ?? createAgentRequest.PartyOrgNo,
            OrgNo = createAgentRequest.PartyOrgNo,
            SystemId = createAgentRequest.SystemId,
        };

        RegisteredSystemResponse? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(createAgentRequest.SystemId);
        if (systemInfo is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<bool> valRef = await ValidateExternalRequestId(externalRequestId);
        if (valRef.IsProblem)
        {
            return valRef.Problem;
        }

        Result<bool> valVendor = ValidateVendorOrgNo(vendorOrgNo, systemInfo);
        if (valVendor.IsProblem)
        {
            return valVendor.Problem;
        }

        Result<bool> valCust = await ValidateCustomerOrgNo(createAgentRequest.PartyOrgNo);
        if (valCust.IsProblem)
        {
            return valCust.Problem;
        }

        if (createAgentRequest.RedirectUrl is not null && createAgentRequest.RedirectUrl != string.Empty)
        {
            var valRedirect = AuthenticationHelper.ValidateRedirectUrl(createAgentRequest.RedirectUrl, systemInfo.AllowedRedirectUrls);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }

        if (createAgentRequest.AccessPackages is null || createAgentRequest.AccessPackages.Count == 0)
        {
            return Problem.AccessPackage_ValidationFailed;
        }

        Result<bool> valPackages = ValidateAccessPackages(createAgentRequest.AccessPackages, systemInfo);
        if (valPackages.IsProblem)
        {
            return valPackages.Problem;
        }

        // Set an empty ExternalRef to be equal to the PartyOrgNo
        if (createAgentRequest.ExternalRef is null || createAgentRequest.ExternalRef == string.Empty)
        {
            createAgentRequest.ExternalRef = createAgentRequest.PartyOrgNo;
        }

        Guid newId = Guid.NewGuid();

        var created = new AgentRequestSystemResponse()
        {
            Id = newId,
            ExternalRef = createAgentRequest.ExternalRef,
            SystemId = createAgentRequest.SystemId,
            PartyOrgNo = createAgentRequest.PartyOrgNo,
            AccessPackages = createAgentRequest.AccessPackages,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createAgentRequest.RedirectUrl,
            UserType = Core.Enums.SystemUserType.Agent
        };

        Result<bool> res = await requestRepository.CreateAgentRequest(created);
        if (res.IsProblem)
        {
            return Problem.RequestCouldNotBeStored;
        }

        return created;
    }

    /// <summary>
    /// Validate that the Package is both a subset of the Default Packages registered on the System, and at least one Package is selected.
    /// </summary>
    /// <param name="accessPackages">the AccessPackages chosen for the Request</param>
    /// <param name="systemInfo">The Vendor's Registered System</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateAccessPackages(List<AccessPackage> accessPackages, RegisteredSystemResponse systemInfo)
    {
        if (systemInfo == null || systemInfo.AccessPackages == null)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        if (systemInfo.AccessPackages.Count == 0)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        if (accessPackages.Count > systemInfo.AccessPackages.Count)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        bool[] validate = new bool[accessPackages.Count];
        foreach (AccessPackage accessPackage in accessPackages) 
        {
            foreach ( AccessPackage systemPackage in systemInfo.AccessPackages)
            {
                if (accessPackage.Urn == systemPackage.Urn) 
                {
                    validate[accessPackages.IndexOf(systemPackage)] = true;
                }
            }
        }

        foreach (bool package in validate)
        {
            if (!package)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }
        }

        return true;
    }

    /// <summary>
    /// Validate that the Rights is both a subset of the Default Rights registered on the System, and at least one Right is selected.
    /// Also ensure that if any of the new Rights have sub-resources, that the sub-resources are equal to the registered Rights.
    /// </summary>
    /// <param name="rights">the Rights chosen for the Request</param>
    /// <param name="systemInfo">The Vendor's Registered System</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateRights(List<Right> rights, RegisteredSystemResponse systemInfo)
    {
        if (rights.Count > 0 && systemInfo.Rights is null)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        if (rights.Count > systemInfo.Rights?.Count)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        bool[] validate = new bool[rights.Count];
        foreach (var requestRight in rights)
        {
            // Find the first matching Right in the list of Rights, with a matching TOP level AttributePair in the Resource list
            List<Right> topMatchesInSystem = FindListOfMatchingRightsOnTopAttribute(requestRight.Resource[0], systemInfo.Rights);
            if (topMatchesInSystem.Count == 0)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }

            // Locate one full match, the first we find might not be the correct
            foreach (var systemRight in topMatchesInSystem)
            {
                if (IsFullMatch(systemRight, requestRight))
                {
                    validate[rights.IndexOf(requestRight)] = true;
                }
            }    
        }

        foreach (bool right in validate)
        {
            if (!right)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }
        }

        return true;
    }

    private static bool IsFullMatch(Right systemRight, Right requestRight)
    {
        if (requestRight.Resource.Count != systemRight.Resource.Count)
        {
            return false;
        }

        foreach (var systemPair in systemRight.Resource)
        {
            if (!VerifySubResource(systemPair, requestRight))
            {
                return false;
            }
        }

        return true;
    }

    // Ensure that the system's sub-resource is present in the request's list of sub-resources
    private static bool VerifySubResource(AttributePair system, Right request)
    {
        foreach (var resource in request.Resource)
        {
            if (system.Id == resource.Id && system.Value == resource.Value)
            {
                return true;
            }
        }

        return false;
    }

    // Find the first matching Right in the list of Rights, with a matching TOP level AttributePair in the Resource list
    private static List<Right> FindListOfMatchingRightsOnTopAttribute(AttributePair newpair, List<Right> systemlist)
    {
        List<Right> list = [];

        foreach (Right systemRight in systemlist)
        {
            foreach (AttributePair p in systemRight.Resource)
            {
                if (newpair.Id == p.Id && newpair.Value == p.Value)
                {
                    list.Add(systemRight);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Validate that the combination of SystemId, PartyOrg and External ref does not currently exist in the active Request table (not soft-deleted).
    /// If a pending Request exists with the same ExternalRequestId, we return the pending Request.
    /// If an active SystemUser exists with the same ExternalRequestId, we return a Problem.
    /// If the id's refer to a Rejected or Denied Request, we return a BadRequest, and ask to delete and renew the Request.
    /// </summary>
    /// <param name="externalRequestId">Combination of SystemId, PartyOrg and External Ref</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateExternalRequestId(ExternalRequestId externalRequestId)
    {
        RequestSystemResponse? res = await requestRepository.GetRequestByExternalReferences(externalRequestId);

        if (res is not null && res.Status == RequestStatus.Accepted.ToString())
        {
            return Problem.ExternalRequestIdAlreadyAccepted;
        }

        if (res is not null && res.Status == RequestStatus.New.ToString())
        {
            return Problem.ExternalRequestIdPending;
        }

        if (res is not null && res.Status == RequestStatus.Denied.ToString())
        {
            return Problem.ExternalRequestIdDenied;
        }

        if (res is not null && res.Status == RequestStatus.Rejected.ToString())
        {
            return Problem.ExternalRequestIdRejected;
        }

        return true;
    }

    /// <summary>
    /// Validate that the Vendor's OrgNo owns the chosen SystemId (which was retrieved from the token in the controller)
    /// </summary>
    /// <param name="vendorOrgNo">Vendor's OrgNo</param>
    /// <param name="sys">The chosen System Info</param>
    /// <returns>Result or Problem</returns>
    private Result<bool> ValidateVendorOrgNo(OrganisationNumber vendorOrgNo, RegisteredSystemResponse sys)
    {
        OrganisationNumber? systemOrgNo = null;

        if (sys is not null)
        {
            systemOrgNo = OrganisationNumber.CreateFromStringOrgNo(sys.SystemVendorOrgNumber);
        }

        if (vendorOrgNo != systemOrgNo)
        {
            return Problem.SystemIdNotFound;
        }

        if (sys is not null && systemOrgNo == vendorOrgNo)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validate the PartyOrgNo for the Customer towards ER
    /// </summary>
    /// <param name="partyOrgNo">the PartyOrgNo for the Customer</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateCustomerOrgNo(string partyOrgNo)
    {        
        if (partyOrgNo == null) 
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        Organization? org = await partiesClient.GetOrganizationAsync(partyOrgNo);
        if (org is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponse>> GetRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo)
    {
        RequestSystemResponse? res = await requestRepository.GetRequestByExternalReferences(externalRequestId);

        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(externalRequestId.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }

        return new RequestSystemResponse()
        {
            Id = res.Id,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            Rights = res.Rights,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl
        };
    }

    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponse>> GetRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
    {
        RequestSystemResponse? res = await requestRepository.GetRequestByInternalId(requestId);
        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(res.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }                

        return new RequestSystemResponse()
        {
            Id = res.Id,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            Rights = res.Rights,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl
        };
    }

    /// <inheritdoc/>
    public async Task<Result<AgentRequestSystemResponse>> GetAgentRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
    {
        AgentRequestSystemResponse? res = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        if (res.AccessPackages == null)
        {
            return Problem.NotAnAgentRequest;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(res.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }

        return new AgentRequestSystemResponse()
        {
            Id = res.Id,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl
        };
    }

    private async Task<Result<bool>> RetrieveChosenSystemInfoAndValidateVendorOrgNo(string systemId, OrganisationNumber vendorOrgNo)
    {
        RegisteredSystemResponse? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(systemId);
        if (systemInfo is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<bool> valVendor = ValidateVendorOrgNo(vendorOrgNo, systemInfo);
        if (valVendor.IsProblem)
        {
            return valVendor.Problem;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponse>> GetRequestByPartyAndRequestId(int partyId, Guid requestId)
    {
        Party party = await partiesClient.GetPartyAsync(partyId);
        if (party is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        RequestSystemResponse? find = await requestRepository.GetRequestByInternalId(requestId);
        if (find is null)
        {
            return Problem.RequestNotFound;
        }

        if (party.OrgNumber != find.PartyOrgNo)
        {
            return Problem.RequestNotFound;
        }

        var request = new RequestSystemResponse
        {
            Id = find.Id,
            SystemId = find.SystemId,
            ExternalRef = find.ExternalRef,
            Rights = find.Rights,
            AccessPackages = find.AccessPackages,
            PartyOrgNo = find.PartyOrgNo,
            Status = find.Status,
            RedirectUrl = find.RedirectUrl
        };

        return request;
    }

    /// <inheritdoc/>
    public async Task<Result<AgentRequestSystemResponse>> GetAgentRequestByPartyAndRequestId(int partyId, Guid requestId)
    {
        Party party = await partiesClient.GetPartyAsync(partyId);
        if (party is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        AgentRequestSystemResponse? find = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (find is null)
        {
            return Problem.RequestNotFound;
        }

        if (party.OrgNumber != find.PartyOrgNo)
        {
            return Problem.RequestNotFound;
        }

        var request = new AgentRequestSystemResponse
        {
            Id = find.Id,
            SystemId = find.SystemId,
            ExternalRef = find.ExternalRef,
            AccessPackages = find.AccessPackages,
            PartyOrgNo = find.PartyOrgNo,
            Status = find.Status,
            RedirectUrl = find.RedirectUrl
        };

        return request;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAndCreateSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
    {
        Result<bool> validatePartyRequest = await ValidatePartyRequest(partyId, requestId, SystemUserType.Standard, cancellationToken);
        if (validatePartyRequest.IsProblem) 
        {
            return validatePartyRequest.Problem;
        }

        RequestSystemResponse? systemUserRequest = await requestRepository.GetRequestByInternalId(requestId);
        if (systemUserRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        RegisteredSystemResponse? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserRequest.SystemId);
        if (regSystem is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<SystemUser> toBeInserted = MapSystemUserRequestToSystemUser(systemUserRequest, regSystem, partyId);
        if (toBeInserted.IsProblem)
        {
            return toBeInserted.Problem;
        }

        DelegationCheckResult delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(partyId, regSystem.Id, systemUserRequest.Rights, false, cancellationToken);
        if (delegationCheckFinalResult.RightResponses is null)
        {
            // This represents some problem with doing the delegation check beyond the rights not being delegable.
            return Problem.UnableToDoDelegationCheck;
        }

        if (!delegationCheckFinalResult.CanDelegate)
        {
            // This represents that the rights are not delegable, but the DelegationCheck method call has been completed.
            return DelegationHelper.MapDetailExternalErrorListToProblemInstance(delegationCheckFinalResult.errors);
        }

        Guid? systemUserId = await requestRepository.ApproveAndCreateSystemUser(requestId, toBeInserted.Value, userId, cancellationToken);

        if (systemUserId is null)
        {
            return Problem.SystemUser_FailedToCreate;
        }

        toBeInserted.Value.Id = systemUserId.ToString()!;

        Result<bool> delegationSucceeded = await accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), toBeInserted.Value, delegationCheckFinalResult.RightResponses);
        if (delegationSucceeded.IsProblem) 
        { 
            return delegationSucceeded.Problem; 
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAndCreateAgentSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
    {
        Result<bool> validatePartyRequest = await ValidatePartyRequest(partyId, requestId, SystemUserType.Agent, cancellationToken);
        if (validatePartyRequest.IsProblem)
        {
            return validatePartyRequest.Problem;
        }

        AgentRequestSystemResponse? systemUserRequest = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (systemUserRequest is null)
        {
            return Problem.AgentRequestNotFound;
        }

        if (systemUserRequest.AccessPackages == null)
        {
            return Problem.NotAnAgentRequest;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        RegisteredSystemResponse? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserRequest.SystemId);
        if (regSystem is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<SystemUser> toBeInserted = MapAgentSystemUserRequestToSystemUser(systemUserRequest, regSystem, partyId);
        if (toBeInserted.IsProblem)
        {
            return toBeInserted.Problem;
        }

        Guid? systemUserId = await requestRepository.ApproveAndCreateSystemUser(requestId, toBeInserted.Value, userId, cancellationToken);

        if (systemUserId is null)
        {
            return Problem.SystemUser_FailedToCreate;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectSystemUser(int partyId, Guid requestId, int userId, CancellationToken cancellationToken)
    {
        Result<bool> validatePartyRequest = await ValidatePartyRequest(partyId, requestId, SystemUserType.Standard, cancellationToken);
        if (validatePartyRequest.IsProblem)
        {
            return validatePartyRequest.Problem;
        }

        RequestSystemResponse? systemUserRequest = await requestRepository.GetRequestByInternalId(requestId);
        if (systemUserRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        return await requestRepository.RejectSystemUser(requestId, userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectAgentSystemUser(int partyId, Guid requestId, int userId, CancellationToken cancellationToken)
    {
        Result<bool> validatePartyRequest = await ValidatePartyRequest(partyId, requestId, SystemUserType.Agent, cancellationToken);
        if (validatePartyRequest.IsProblem)
        {
            return validatePartyRequest.Problem;
        }

        AgentRequestSystemResponse? systemUserRequest = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (systemUserRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        return await requestRepository.RejectSystemUser(requestId, userId, cancellationToken);
    }

    private static Result<SystemUser> MapSystemUserRequestToSystemUser(RequestSystemResponse systemUserRequest, RegisteredSystemResponse regSystem, int partyId)
    {
        SystemUser? toBeInserted = null;
        regSystem.Name.TryGetValue("nb", out string? systemName);
        if (systemName is null) 
        {
            return Problem.SystemNameNotFound;
        }

        if (systemUserRequest != null)
        {
            toBeInserted = new SystemUser
            {
                SystemId = systemUserRequest.SystemId,
                IntegrationTitle = systemName,
                SystemInternalId = regSystem?.InternalId,
                PartyId = partyId.ToString(),
                ReporteeOrgNo = systemUserRequest.PartyOrgNo,
                ExternalRef = systemUserRequest.ExternalRef ?? systemUserRequest.PartyOrgNo,
                UserType = Core.Enums.SystemUserType.Standard
            };
        }

        return toBeInserted!;
    }

    private static Result<SystemUser> MapAgentSystemUserRequestToSystemUser(AgentRequestSystemResponse agentSystemUserRequest, RegisteredSystemResponse regSystem, int partyId)
    {
        SystemUser? toBeInserted = null;
        regSystem.Name.TryGetValue("nb", out string? systemName);
        if (systemName is null)
        {
            return Problem.SystemNameNotFound;
        }

        if (agentSystemUserRequest != null)
        {
            toBeInserted = new SystemUser
            {
                SystemId = agentSystemUserRequest.SystemId,
                IntegrationTitle = systemName,
                SystemInternalId = regSystem?.InternalId,
                PartyId = partyId.ToString(),
                ReporteeOrgNo = agentSystemUserRequest.PartyOrgNo,
                ExternalRef = agentSystemUserRequest.ExternalRef ?? agentSystemUserRequest.PartyOrgNo,
                AccessPackages = agentSystemUserRequest.AccessPackages,
                UserType = Core.Enums.SystemUserType.Agent
            };
        }

        return toBeInserted!;
    }

    /// <summary>
    /// Removes requested Rights that are not in the System's list of prepared Rights
    /// </summary>
    /// <param name="rights">the Requested Rights</param>
    /// <param name="systemId">the system id</param>
    /// <param name="cancellationToken">cancellation </param>
    /// <returns></returns>
    private async Task<List<Right>> VerifySubsetOfRights(List<Right> rights, string systemId, CancellationToken cancellationToken)
    {
        List<Right> rightsInSystem = await systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);

        List<Right> verifiedRights = [];

        foreach (var right in rights)
        {
            foreach (var rightInSystem in rightsInSystem)
            {
                if (Right.Compare(right, rightInSystem))
                {
                    verifiedRights.Add(right);
                }
            }
        }

        return verifiedRights;
    }

    private static (bool CanDelegate, List<DetailExternal> Errors) ResolveIfHasAccess(List<DelegationResponseData> rightResponse)
    {
        List<DetailExternal> errors = [];
        var canDelegate = true;

        foreach (var data in rightResponse)
        {
            if (data.Status != "Delegable")
            { 
                errors.AddRange(data.Details);
                canDelegate = false;
            }
        }

        return (canDelegate, errors);
    }

    /// <inheritdoc/>
    public async Task<Result<Page<RequestSystemResponse, Guid>>> GetAllRequestsForVendor(
        OrganisationNumber vendorOrgNo,
        string systemId,
        Page<Guid>.Request continueRequest,
        CancellationToken cancellationToken)
    {
        RegisteredSystemResponse? system = await systemRegisterRepository.GetRegisteredSystemById(systemId);
        if (system is null)
        {
            return Problem.SystemIdNotFound;
        }

        // Verify that the orgno from the logged on token owns this system
        if (OrganisationNumber.CreateFromStringOrgNo(system.SystemVendorOrgNumber) != vendorOrgNo)
        {
            return Problem.SystemIdNotFound;
        }
        
        List<RequestSystemResponse>? theList = await requestRepository.GetAllRequestsBySystem(systemId, cancellationToken);
        theList ??= [];

        return Page.Create(theList, _paginationSize, static theList => theList.Id); 
    }

    /// <inheritdoc/>
    public async Task<Result<Page<AgentRequestSystemResponse, Guid>>> GetAllAgentRequestsForVendor(
        OrganisationNumber vendorOrgNo,
        string systemId,
        Page<Guid>.Request continueRequest,
        CancellationToken cancellationToken)
    {
        RegisteredSystemResponse? system = await systemRegisterRepository.GetRegisteredSystemById(systemId);
        if (system is null)
        {
            return Problem.SystemIdNotFound;
        }

        // Verify that the orgno from the logged on token owns this system
        if (OrganisationNumber.CreateFromStringOrgNo(system.SystemVendorOrgNumber) != vendorOrgNo)
        {
            return Problem.SystemIdNotFound;
        }

        List<AgentRequestSystemResponse>? theList = await requestRepository.GetAllAgentRequestsBySystem(systemId, cancellationToken);
        theList ??= [];

        return Page.Create(theList, _paginationSize, static theList => theList.Id);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeleteRequestByRequestId(Guid requestId)
    {
        var result = await requestRepository.DeleteRequestByRequestId(requestId);
        if (result)
        {
            return true;
        }

        return Problem.RequestNotFound;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetRedirectByRequestId(Guid requestId)
    {
        RequestSystemResponse? systemUserRequest = await requestRepository.GetRequestByInternalId(requestId);
        if (systemUserRequest is null || systemUserRequest.RedirectUrl is null)
        {
            return Problem.RequestNotFound;
        }

        return systemUserRequest.RedirectUrl;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetRedirectByAgentRequestId(Guid requestId)
    {
        AgentRequestSystemResponse? agentRequest = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (agentRequest is null || agentRequest.RedirectUrl is null)
        {
            return Problem.RequestNotFound;
        }

        return agentRequest.RedirectUrl;
    }

    /// <inheritdoc/>
    public async Task<Result<AgentRequestSystemResponse>> GetAgentRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo)
    {
        AgentRequestSystemResponse? res = await requestRepository.GetAgentRequestByExternalReferences(externalRequestId);

        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(externalRequestId.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }

        return res;
    }

    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponseInternal>> CheckUserAuthorizationAndGetAgentRequest(Guid requestId)
    {
        AgentRequestSystemResponse? request = await requestRepository.GetAgentRequestByInternalId(requestId);
        if (request is null)
        {
            return Problem.RequestNotFound;
        }

        Result<Party> validatedParty = await ValidateAndVerifyRequest(request.PartyOrgNo);
        if (validatedParty.IsProblem)
        {
            return validatedParty.Problem;
        }

        return new RequestSystemResponseInternal()
        {
            Id = request.Id,
            ExternalRef = request.ExternalRef,
            SystemId = request.SystemId,
            PartyOrgNo = request.PartyOrgNo,
            PartyId = validatedParty.Value.PartyId,
            PartyUuid = (Guid)validatedParty.Value.PartyUuid!,
            Rights = [],
            AccessPackages = request.AccessPackages,
            Status = request.Status,
            ConfirmUrl = request.ConfirmUrl,
            Created = request.Created,
            RedirectUrl = request.RedirectUrl,
            SystemUserType = request.UserType.ToString()
        };
    }

    private async Task<Result<Party>> ValidateAndVerifyRequest(string orgNo)
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return Problem.RequestNotFound;
        }

        IEnumerable<Claim> claims = context.User.Claims;

        Party party = await partiesClient.GetPartyByOrgNo(orgNo);

        if (!party.PartyUuid.HasValue)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        Guid partyUuid = (Guid)party.PartyUuid;

        XacmlJsonRequestRoot jsonRequest = SpecificDecisionHelper.CreateDecisionRequestForUserId(claims, "write", "altinn_access_management", partyUuid);

        XacmlJsonResponse response = await pdp.GetDecisionForRequest(jsonRequest);
        if (response is null)
        {
            return Problem.RequestNotFound;
        }

        if (SpecificDecisionHelper.ValidatePdpDecision(response, context.User))
        {
            return party;
        }

        return Problem.RequestNotFound;
    }

    /// <inheritdoc/>
    public async Task<Result<RequestSystemResponseInternal>> CheckUserAuthorizationAndGetRequest(Guid requestId)
    {
        RequestSystemResponse? request = await requestRepository.GetRequestByInternalId(requestId);
        if (request is null)
        {
            return Problem.RequestNotFound;
        }

        Result<Party> validatedParty = await ValidateAndVerifyRequest(request.PartyOrgNo);
        if (validatedParty.IsProblem)
        {
            return validatedParty.Problem;
        }

        return new RequestSystemResponseInternal()
            {
                Id = request.Id,
                ExternalRef = request.ExternalRef,
                SystemId = request.SystemId,
                PartyOrgNo = request.PartyOrgNo,
                PartyId = validatedParty.Value.PartyId,
                PartyUuid = (Guid)validatedParty.Value.PartyUuid!,
                Rights = request.Rights,
                AccessPackages = request.AccessPackages,
                Status = request.Status,
                ConfirmUrl = request.ConfirmUrl,
                Created = request.Created,
                RedirectUrl = request.RedirectUrl
            };       
    }

    private async Task<Result<bool>> ValidatePartyRequest(int partyId, Guid requestId, SystemUserType userType,CancellationToken cancellationToken)
    {
        Party party = await partiesClient.GetPartyAsync(partyId, cancellationToken);
        if (party is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        if (userType == SystemUserType.Agent)
        {
            AgentRequestSystemResponse? find = await requestRepository.GetAgentRequestByInternalId(requestId);
            if (find is null)
            {
                return Problem.AgentRequestNotFound;
            }

            if (party.OrgNumber != find.PartyOrgNo)
            {
                return Problem.PartyId_AgentRequest_Mismatch;
            }
        }

        if (userType == SystemUserType.Standard)
        {
            RequestSystemResponse? find = await requestRepository.GetRequestByInternalId(requestId);
            if (find is null)
            {
                return Problem.RequestNotFound;
            }

            if (party.OrgNumber != find.PartyOrgNo)
            {
                return Problem.PartyId_Request_Mismatch;
            }
        }

        return true;
    }
}
