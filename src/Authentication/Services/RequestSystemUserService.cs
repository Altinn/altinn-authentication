using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services;
#nullable enable

/// <inheritdoc/>
public class RequestSystemUserService(
    ISystemRegisterService systemRegisterService,
    IPartiesClient partiesClient,
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
    /// Validate that the Rights is both a subset of the Default Rights registered on the System, and at least one Right is selected.
    /// Also ensure that if any of the new Rights have sub-resources, that the sub-resources are equal to the registered Rights.
    /// </summary>
    /// <param name="rights">the Rights chosen for the Request</param>
    /// <param name="systemInfo">The Vendor's Registered System</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateRights(List<Right> rights, RegisteredSystem systemInfo)
    {
        if (rights.Count == 0 || systemInfo.Rights.Count == 0)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        if (rights.Count > systemInfo.Rights.Count)
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

        return Problem.RedirectUriNotFound;
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
    public async Task<Result<bool>> ApproveAndCreateSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
    {
        RequestSystemResponse? systemUserRequest = await requestRepository.GetRequestByInternalId(requestId);
        if (systemUserRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        RegisteredSystem? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserRequest.SystemId);
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
    public async Task<Result<bool>> RejectSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
    {
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

    private static Result<SystemUser> MapSystemUserRequestToSystemUser(RequestSystemResponse systemUserRequest, RegisteredSystem regSystem, int partyId)
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
                ExternalRef = systemUserRequest.ExternalRef ?? systemUserRequest.PartyOrgNo
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
}
