using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services;
#nullable enable

/// <inheritdoc/>
public class RequestSystemUserService(
    IHttpContextAccessor httpContextAccessor,
    ISystemRegisterService systemRegisterService,
    IPartiesClient partiesClient,
    IAccessManagementClient _accessManagemetClient,
    IPDP pdp,
    ISystemRegisterRepository systemRegisterRepository,
    IRequestRepository requestRepository,
    IOptions<PaginationOptions> _paginationOption,
    ISystemUserService systemUserService)
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

        if (createRequest.Rights is null && createRequest.AccessPackages is null)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        bool hasAtLeastOne = false;

        // Must have a minimum of either one Right or one Accesspackage for a Standard SystemUser
        if (createRequest.Rights is not null && createRequest.Rights.Count > 0)
        {
            hasAtLeastOne = true;
        }

        if (createRequest.AccessPackages is not null && createRequest.AccessPackages.Count > 0)
        {
            hasAtLeastOne = true;
        }

        if (!hasAtLeastOne)
        {
            return Problem.SystemUser_MissingRightsOrAccessPackages;
        }

        if (createRequest.Rights is not null && createRequest.Rights.Count > 0)
        {
            Result<bool> valRights = systemUserService.ValidateRights(createRequest.Rights, systemInfo);
            if (valRights.IsProblem)
            {
                return valRights.Problem;
            }
        }

        if (createRequest.AccessPackages is not null && createRequest.AccessPackages.Count > 0)
        {
            Result<bool> valPackages = await systemUserService.ValidateAccessPackages(createRequest.AccessPackages, systemInfo, isAgentRequest: false);
            if (valPackages.IsProblem)
            {
                return valPackages.Problem;
            }                        
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
            IntegrationTitle = createRequest.IntegrationTitle?.Trim() ?? (systemInfo.Name.TryGetValue("nb", out string? value) ? value : null),
            ExternalRef = createRequest.ExternalRef,
            SystemId = createRequest.SystemId,
            PartyOrgNo = createRequest.PartyOrgNo,
            Rights = createRequest.Rights ?? [],
            AccessPackages = createRequest.AccessPackages ?? [],
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createRequest.RedirectUrl,
            TimedOut = false
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
            return Problem.SystemUser_MissingAccessPackage;
        }

        Result<bool> valPackages = await systemUserService.ValidateAccessPackages(createAgentRequest.AccessPackages, systemInfo, isAgentRequest: true);
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
            IntegrationTitle = createAgentRequest.IntegrationTitle?.Trim() ?? (systemInfo.Name.TryGetValue("nb", out string? value) ? value : null),
            ExternalRef = createAgentRequest.ExternalRef,
            SystemId = createAgentRequest.SystemId,
            PartyOrgNo = createAgentRequest.PartyOrgNo,
            AccessPackages = createAgentRequest.AccessPackages,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createAgentRequest.RedirectUrl,
            UserType = Core.Enums.SystemUserType.Agent,
            TimedOut = false
        };

        Result<bool> res = await requestRepository.CreateAgentRequest(created);
        if (res.IsProblem)
        {
            return Problem.RequestCouldNotBeStored;
        }

        return created;
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
    private static Result<bool> ValidateVendorOrgNo(OrganisationNumber vendorOrgNo, RegisteredSystemResponse sys)
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
            IntegrationTitle = res.IntegrationTitle,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            Rights = res.Rights,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            Created = res.Created,
            RedirectUrl = res.RedirectUrl,
            TimedOut = res.TimedOut
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
            IntegrationTitle = res.IntegrationTitle,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            Rights = res.Rights,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            Created = res.Created,
            RedirectUrl = res.RedirectUrl,
            TimedOut = res.TimedOut
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
            IntegrationTitle = res.IntegrationTitle,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            AccessPackages = res.AccessPackages,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl,
            TimedOut = res.TimedOut
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
            IntegrationTitle = find.IntegrationTitle,
            SystemId = find.SystemId,
            ExternalRef = find.ExternalRef,
            Rights = find.Rights,
            AccessPackages = find.AccessPackages,
            PartyOrgNo = find.PartyOrgNo,
            Status = find.Status,
            Created = find.Created,
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
            IntegrationTitle = find.IntegrationTitle,
            SystemId = find.SystemId,
            ExternalRef = find.ExternalRef,
            AccessPackages = find.AccessPackages,
            PartyOrgNo = find.PartyOrgNo,
            Status = find.Status,
            Created = find.Created,
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

        Result<SystemUserInternalDTO> systemUser = await systemUserService.CreateSystemUserFromApprovedVendorRequest(systemUserRequest, partyId.ToString(), userId, cancellationToken);
        if (systemUser.IsProblem)
        {
            return systemUser.Problem;
        }

        // Logs the change in the Request Repository
        bool sat = await requestRepository.SetRequestApproved(requestId, new Guid(systemUser.Value.Id), userId, cancellationToken);
        if (!sat)
        {
            return Problem.RequestCouldNotBeUpdated;
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

        RegisteredSystemResponse? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserRequest.SystemId, cancellationToken);
        if (regSystem is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<SystemUserInternalDTO> toBeInserted = MapAgentSystemUserRequestToSystemUser(systemUserRequest, regSystem, partyId);
        if (toBeInserted.IsProblem)
        {
            return toBeInserted.Problem;
        }

        Result<SystemUserInternalDTO> res = await systemUserService.CreateSystemUserFromApprovedVendorRequest(systemUserRequest, partyId.ToString(), userId, cancellationToken);
        if (res.IsProblem)
        {
            return res.Problem;
        }

        bool sat = await requestRepository.SetRequestApproved(requestId, new Guid(toBeInserted.Value.Id), userId, cancellationToken);
        if (!sat)
        {
            return Problem.RequestCouldNotBeUpdated;
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

    private static Result<SystemUserInternalDTO> MapSystemUserRequestToSystemUser(RequestSystemResponse systemUserRequest, RegisteredSystemResponse regSystem, int partyId)
    {
        SystemUserInternalDTO? toBeInserted = null;
        regSystem.Name.TryGetValue("nb", out string? systemName);
        if (systemName is null) 
        {
            return Problem.SystemNameNotFound;
        }

        if (systemUserRequest != null)
        {
            toBeInserted = new SystemUserInternalDTO
            {
                SystemId = systemUserRequest.SystemId,
                IntegrationTitle = systemUserRequest.IntegrationTitle ?? systemName,
                SystemInternalId = regSystem?.InternalId,
                PartyId = partyId.ToString(),
                ReporteeOrgNo = systemUserRequest.PartyOrgNo,
                ExternalRef = systemUserRequest.ExternalRef ?? systemUserRequest.PartyOrgNo,
                UserType = Core.Enums.SystemUserType.Standard
            };
        }

        return toBeInserted!;
    }

    private static Result<SystemUserInternalDTO> MapAgentSystemUserRequestToSystemUser(AgentRequestSystemResponse agentSystemUserRequest, RegisteredSystemResponse regSystem, int partyId)
    {
        SystemUserInternalDTO? toBeInserted = null;
        regSystem.Name.TryGetValue("nb", out string? systemName);
        if (systemName is null)
        {
            return Problem.SystemNameNotFound;
        }

        if (agentSystemUserRequest != null)
        {
            toBeInserted = new SystemUserInternalDTO
            {
                Id = agentSystemUserRequest.Id.ToString(),
                SystemId = agentSystemUserRequest.SystemId,
                IntegrationTitle = agentSystemUserRequest.IntegrationTitle ?? systemName,
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
        RegisteredSystemResponse? system = await systemRegisterRepository.GetRegisteredSystemById(systemId, cancellationToken);
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
        RegisteredSystemResponse? system = await systemRegisterRepository.GetRegisteredSystemById(systemId, cancellationToken);
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

        return Problem.Request_UserIsNotAccessManager;
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

    private async Task<Result<bool>> ValidatePartyRequest(int partyId, Guid requestId, SystemUserType userType, CancellationToken cancellationToken)
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

    /// <inheritdoc/>
    public async Task<Result<bool>> EscalateApprovalSystemUser(Guid requestId, int party, int userId, CancellationToken cancellationToken)
    {
        return await requestRepository.SetRequestEscalated(requestId,userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> EscalateApprovalAgentSystemUser(Guid requestId, int party, int userId, CancellationToken cancellationToken)
    {
        return await requestRepository.SetRequestEscalated(requestId, userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<List<RequestSystemResponse>>> GetPendingStandardRequests(string orgno, int userId, CancellationToken cancellationToken)
    {
        List<RequestSystemResponse> theList = [];
        Result<List<RequestSystemResponse>> result = await requestRepository.GetAllPendingStandardRequests(orgno,cancellationToken);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        return theList;
    }

    /// <inheritdoc/>
    public async Task<Result<List<AgentRequestSystemResponse>>> GetPendingAgentRequests(string orgno, int userId, CancellationToken cancellationToken)
    {
        List<AgentRequestSystemResponse> theList = [];
        Result<List<AgentRequestSystemResponse>> result = await requestRepository.GetAllPendingAgentRequests(orgno, cancellationToken);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        return theList;
    }
}
