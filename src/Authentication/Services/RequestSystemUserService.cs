using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services;
#nullable enable

/// <inheritdoc/>
public class RequestSystemUserService(
    ISystemRegisterService systemRegisterService,
    IPartiesClient partiesClient,
    ISystemRegisterRepository systemRegisterRepository,
    IAccessManagementClient accessManagementClient,
    IRequestRepository requestRepository)
    : IRequestSystemUser
{
    /// <summary>
    /// Used to limit the number of items returned in a paginated list
    /// </summary>
    public int PaginationSize { get; set; } = 3;

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

        RegisteredSystem? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(createRequest.SystemId);
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
            var valRedirect = ValidateRedirectUrl(createRequest.RedirectUrl, systemInfo);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }

        Result<bool> valRights = ValidateRights(createRequest.Rights, systemInfo);
        if (valRights.IsProblem)
        {
            return valRights.Problem;
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

    /// <summary>
    /// Validate that the Rights is both a subset of the Default Rights registered on the System, and at least one Right is selected
    /// </summary>
    /// <param name="rights">the Rights chosen for the Request</param>
    /// <returns>Result or Problem</returns>
    private Result<bool> ValidateRights(List<Right> rights, RegisteredSystem systemInfo)
    {
        if (rights.Count == 0 || systemInfo.Rights.Count == 0)
        {
            return false;
        }

        if (rights.Count > systemInfo.Rights.Count)
        {
            return false;
        }

        bool[] validate = new bool[rights.Count];
        int i = 0;
        foreach (var rightRequest in rights)
        {
            foreach (var resource in rightRequest.Resource)
            {
                if (FindOneAttributePair(resource, systemInfo.Rights))
                {
                    validate[i] = true;
                    break;
                }
            }

            i++;
        }

        foreach (bool right in validate)
        {
            if (!right)
            {
                return false;
            }
        }

        return true;
    }

    private static bool FindOneAttributePair(AttributePair pair, List<Right> list)
    {
        foreach (Right l in list)
        {
            foreach (AttributePair p in l.Resource)
            {
                if (pair.Id == p.Id && pair.Value == p.Value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Validate that the RedirectUrl chosen is the same as one of the RedirectUrl's listed for the Registered System
    /// </summary>
    /// <param name="redirectURL">the RedirectUrl chosen</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateRedirectUrl(string redirectURL, RegisteredSystem systemInfo)
    {
        List<Uri> redirectUrlsInSystem = systemInfo.AllowedRedirectUrls;
        Uri chosenUri = new(redirectURL);

        foreach (var uri in redirectUrlsInSystem)
        {
            if (uri.AbsoluteUri == chosenUri.AbsoluteUri)
            {
                return true;
            }
        }

        return true;
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
    private Result<bool> ValidateVendorOrgNo(OrganisationNumber vendorOrgNo, RegisteredSystem sys)
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
            Status = res.Status,
            RedirectUrl = res.RedirectUrl
        };
    }

    private async Task<Result<bool>> RetrieveChosenSystemInfoAndValidateVendorOrgNo(string systemId, OrganisationNumber vendorOrgNo)
    {
        RegisteredSystem? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(systemId);
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
            PartyOrgNo = find.PartyOrgNo,
            Status = find.Status,
            RedirectUrl = find.RedirectUrl
        };

        return request;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAndCreateSystemUser(Guid requestId, int partyId, CancellationToken cancellationToken)
    {
        RequestSystemResponse systemUserRequest = await requestRepository.GetRequestByInternalId(requestId);
        RegisteredSystem? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserRequest.SystemId);
        SystemUser toBeInserted = await MapSystemUserRequestToSystemUser(systemUserRequest, regSystem, partyId);

        DelegationCheckResult delegationCheckFinalResult = await UserDelegationCheckForReportee(partyId, regSystem.SystemId, cancellationToken);
        if (!delegationCheckFinalResult.CanDelegate || delegationCheckFinalResult.RightResponses is null) 
        { 
            return Problem.Rights_NotFound_Or_NotDelegable; 
        }

        Guid? systemUserId = await requestRepository.ApproveAndCreateSystemUser(requestId, toBeInserted, cancellationToken);
        toBeInserted.Id = systemUserId?.ToString();

        Result<bool> delegationSucceeded = await accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), toBeInserted, delegationCheckFinalResult.RightResponses);
        if (delegationSucceeded.IsProblem) 
        { 
            return delegationSucceeded.Problem; 
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectSystemUser(Guid requestId, CancellationToken cancellationToken)
    {
        return await requestRepository.RejectSystemUser(requestId, cancellationToken);
    }

    private async Task<SystemUser> MapSystemUserRequestToSystemUser(RequestSystemResponse systemUserRequest, RegisteredSystem regSystem, int partyId)
    {
        SystemUser toBeInserted = null;
        regSystem.Name.TryGetValue("nb", out string systemName);
        if (systemUserRequest != null)
        {            
            toBeInserted = new SystemUser();
            toBeInserted.SystemId = systemUserRequest.SystemId;
            toBeInserted.IntegrationTitle = systemName;
            toBeInserted.SystemInternalId = regSystem?.SystemInternalId;
            toBeInserted.PartyId = partyId.ToString();
            toBeInserted.ReporteeOrgNo = systemUserRequest.PartyOrgNo;          
        }

        return toBeInserted;
    }

    private async Task<DelegationCheckResult> UserDelegationCheckForReportee(int partyId, string systemId, CancellationToken cancellationToken = default)
    {
        List<Right> rights = await systemRegisterService.GetRightsForRegisteredSystem(systemId, cancellationToken);
        List<RightResponses> rightResponsesList = [];

        foreach (Right right in rights)
        {
            DelegationCheckRequest request = new()
            {
                Resource = right.Resource
            };

            List<DelegationResponseData>? rightResponses = await accessManagementClient.CheckDelegationAccess(partyId.ToString(), request);

            if (rightResponses is null) 
            { 
                return new DelegationCheckResult(false, null); 
            }

            if (!ResolveIfHasAccess(rightResponses)) 
            { 
                return new DelegationCheckResult(false, null); 
            }

            rightResponsesList.Add(new RightResponses(rightResponses));
        }

        return new DelegationCheckResult(true, rightResponsesList);
    }

    private static bool ResolveIfHasAccess(List<DelegationResponseData> rightResponse)
    {
        foreach (var data in rightResponse)
        {
            if (data.Status != "Delegable")
            { 
                return false; 
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<Page<RequestSystemResponse, Guid>>> GetAllRequestsForVendor(
        OrganisationNumber vendorOrgNo,
        string systemId,
        Page<Guid>.Request continueRequest,
        CancellationToken cancellationToken)
    {
        RegisteredSystem? system = await systemRegisterRepository.GetRegisteredSystemById(systemId);
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

        return Page.Create(theList, PaginationSize, static theList => theList.Id); 
    }
}
