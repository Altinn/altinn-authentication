using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authentication.Integration.Clients;
using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services;
#nullable enable

/// <inheritdoc/>
public class ChangeRequestSystemUserService(
    IHttpContextAccessor httpContextAccessor,
    ISystemRegisterService systemRegisterService,
    IPartiesClient partiesClient,
    ISystemRegisterRepository systemRegisterRepository,
    IAccessManagementClient accessManagementClient,
    IChangeRequestRepository changeRequestRepository,
    ISystemUserService systemUserService,
    IResourceRegistryClient resourceRegistryClient,
    IPDP PDPClient,
    IOptions<PaginationOptions> _paginationOption,
    DelegationHelper delegationHelper)
    : IChangeRequestSystemUser
{
    /// <summary>
    /// Used to limit the number of items returned in a paginated list
    /// </summary>
    private int _paginationSize = _paginationOption.Value.Size;

    /// <inheritdoc/>
    public async Task<Result<ChangeRequestResponse>> CreateChangeRequest(ChangeRequestSystemUser createRequest, OrganisationNumber vendorOrgNo, SystemUserInternalDTO systemUser, Guid correllationId)
    {
        // For now we don't support ChangeRequests for an Agent SystemUser
        if (systemUser.UserType == Core.Enums.SystemUserType.Agent)
        {
            return Problem.SystemUserNotFound;
        }

        var created = new ChangeRequestResponse()
        {
            Id = correllationId,
            SystemUserId = new Guid(systemUser.Id),
            ExternalRef = systemUser.ExternalRef,
            SystemId = systemUser.SystemId,
            PartyOrgNo = systemUser.ReporteeOrgNo,
            RequiredRights = createRequest.RequiredRights,
            UnwantedRights = createRequest.UnwantedRights,
            RequiredAccessPackages = createRequest.RequiredAccessPackages,
            UnwantedAccessPackages = createRequest.UnwantedAccessPackages,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createRequest.RedirectUrl           
        };

        Result<RegisteredSystemResponse> regSystem = await ValidateChangeRequest(created, vendorOrgNo, createNew: true);
        if (regSystem.IsProblem)
        {
            return regSystem.Problem;
        }        

        if (createRequest.RedirectUrl is not null && createRequest.RedirectUrl != string.Empty)
        {
            var valRedirect = ValidateRedirectUrl(createRequest.RedirectUrl, regSystem.Value);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }   

        Result<bool> res = await changeRequestRepository.CreateChangeRequest(created);
        if (res.IsProblem)
        {
            return Problem.RequestCouldNotBeStored;
        }

        return created;
    }

    /// <summary>
    /// Validate that the RedirectUrl chosen is the same as one of the RedirectUrl's listed for the Registered System
    /// </summary>
    /// <param name="redirectURL">the RedirectUrl chosen</param>
    /// <param name="systemInfo">The system info</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateRedirectUrl(string redirectURL, RegisteredSystemResponse systemInfo)
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
    /// <param name="correllationId">The id</param>    
    /// <param name="createNew">Set to true if the attempt is to create a new Request, and false for the Approve call</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateStatus(Guid correllationId, bool createNew)
    {
        ChangeRequestResponse? res = await changeRequestRepository.GetChangeRequestById(correllationId);        

        // Attempting to Create a new Change Request, but a pending Request exists, with the same Correllation-Id
        if (createNew && res is not null && res.Status == RequestStatus.New.ToString() && res.Id == correllationId)
        {
            return Problem.ExternalRequestIdPending;
        }

        // Attempting to Create a new Change Request, but the Request has already been Rejected or Accepted (or Denied)
        if (createNew && res is not null && res.Status != RequestStatus.New.ToString() && res.Id == correllationId)
        {
            return Problem.ChangeRequestStatusNotNewUseCorrellationId;
        }

        // Attempting to Approve a Change Request, but the Status is already Accepted 
        if (!createNew && res is not null && res.Status == RequestStatus.Accepted.ToString() && res.Id == correllationId)
        {
            return Problem.ExternalRequestIdAlreadyAccepted;
        }

        // Attemptint to Approve a Change Request, but the Status is already Denied
        if (!createNew && res is not null && res.Status == RequestStatus.Denied.ToString() && res.Id == correllationId)
        {
            return Problem.ExternalRequestIdDenied;
        }

        // Attemptint to Approve a Change Request, but the Status is already Rejected
        if (!createNew && res is not null && res.Status == RequestStatus.Rejected.ToString() && res.Id == correllationId)
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
    public async Task<Result<ChangeRequestResponse>> GetChangeRequestByExternalRef(ExternalRequestId externalRequestId, OrganisationNumber vendorOrgNo)
    {
        ChangeRequestResponse? res = await changeRequestRepository.GetChangeRequestByExternalReferences(externalRequestId);

        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(externalRequestId.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }

        return new ChangeRequestResponse()
        {
            Id = res.Id,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            RequiredRights = res.RequiredRights,
            UnwantedRights = res.UnwantedRights,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl,
            SystemUserId = res.SystemUserId
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ChangeRequestResponse>> GetChangeRequestByGuid(Guid requestId, OrganisationNumber vendorOrgNo)
    {
        ChangeRequestResponse? res = await changeRequestRepository.GetChangeRequestById(requestId);
        if (res is null)
        {
            return Problem.RequestNotFound;
        }

        Result<bool> check = await RetrieveChosenSystemInfoAndValidateVendorOrgNo(res.SystemId, vendorOrgNo);
        if (check.IsProblem)
        {
            return check.Problem;
        }

        return new ChangeRequestResponse()
        {
            Id = res.Id,
            ExternalRef = res.ExternalRef,
            SystemId = res.SystemId,
            PartyOrgNo = res.PartyOrgNo,
            RequiredRights = res.RequiredRights,
            UnwantedRights = res.UnwantedRights,
            RequiredAccessPackages = res.RequiredAccessPackages,
            UnwantedAccessPackages = res.UnwantedAccessPackages,
            Status = res.Status,
            RedirectUrl = res.RedirectUrl,
            SystemUserId = res.SystemUserId            
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
    public async Task<Result<ChangeRequestResponse>> GetChangeRequestByPartyAndRequestId(int partyId, Guid requestId)
    {
        Party party = await partiesClient.GetPartyAsync(partyId);
        if (party is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        ChangeRequestResponse? find = await changeRequestRepository.GetChangeRequestById(requestId);
        if (find is null)
        {
            return Problem.RequestNotFound;
        }

        if (party.OrgNumber != find.PartyOrgNo)
        {
            return Problem.RequestNotFound;
        }

        var request = new ChangeRequestResponse
        {
            Id = find.Id,
            SystemId = find.SystemId,
            ExternalRef = find.ExternalRef,
            RequiredRights = find.RequiredRights,
            UnwantedRights = find.UnwantedRights,
            PartyOrgNo = find.PartyOrgNo,
            RequiredAccessPackages = find.RequiredAccessPackages,
            UnwantedAccessPackages = find.UnwantedAccessPackages,
            Status = find.Status,
            RedirectUrl = find.RedirectUrl,
            SystemUserId = find.SystemUserId
        };

        return request;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAndDelegateChangeOnSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
    {
        ChangeRequestResponse? systemUserChangeRequest = await changeRequestRepository.GetChangeRequestById(requestId);
        if (systemUserChangeRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserChangeRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        RegisteredSystemResponse? regSystem = await systemRegisterRepository.GetRegisteredSystemById(systemUserChangeRequest.SystemId);
        if (regSystem is null)
        {
            return Problem.SystemIdNotFound;
        }

        OrganisationNumber vendor = OrganisationNumber.CreateFromStringOrgNo(regSystem.SystemVendorOrgNumber);

        SystemUserInternalDTO? toBeChanged = await systemUserService.GetSingleSystemUserById(systemUserChangeRequest.SystemUserId);
        if (toBeChanged is null)
        {
            return Problem.SystemUserNotFound;
        }

        Party party = await partiesClient.GetPartyAsync(partyId, cancellationToken);

        if (party is null || string.IsNullOrEmpty(party.OrgNumber))
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        if (!party.PartyUuid.HasValue)
        {
            return Problem.Party_PartyUuid_NotFound;
        }

        Guid partyUuid = party.PartyUuid.Value;

        Result<List<AccessPackage>> verifiedRequiredAccessPackages = await VerifyAccessPackages(systemUserChangeRequest.RequiredAccessPackages, partyUuid, toBeChanged, true, cancellationToken);
        if (verifiedRequiredAccessPackages.IsProblem)
        {
            return verifiedRequiredAccessPackages.Problem;
        }

        Result<List<AccessPackage>> verifiedUnwantedAccessPackages = await VerifyAccessPackages(systemUserChangeRequest.UnwantedAccessPackages, partyUuid, toBeChanged, false, cancellationToken);

        if (verifiedUnwantedAccessPackages.IsProblem)
        {
            return verifiedUnwantedAccessPackages.Problem;
        }

        DelegationCheckResult delegationCheckFinalResult = new(CanDelegate:false, RightResponses:[], errors:[]);

        // Check Single Rights to be added 
        if (systemUserChangeRequest.RequiredRights?.Count > 0)
        {
            delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(partyId, regSystem.Id, systemUserChangeRequest.RequiredRights, false, cancellationToken);
            if (!delegationCheckFinalResult.CanDelegate || delegationCheckFinalResult.RightResponses is null)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }
        }

        // Check AccessPackages to be added
        if (systemUserChangeRequest.RequiredAccessPackages?.Count > 0)
        {
            Result<AccessPackageDelegationCheckResult> checkAccessPackages = await delegationHelper.ValidateDelegationRightsForAccessPackages(partyUuid, regSystem.Id, verifiedRequiredAccessPackages.Value, fromBff: false, cancellationToken);        
            if (checkAccessPackages.IsProblem)   
            {
                return checkAccessPackages.Problem;
            }

            if (checkAccessPackages.Value.CanDelegate && checkAccessPackages.Value.AccessPackages?.Count > 0)
            {
                foreach (AccessPackage accessPackage in checkAccessPackages.Value.AccessPackages)
                {
                    Result<bool> delegationResult = await accessManagementClient.DelegateSingleAccessPackageToSystemUser(partyUuid, Guid.Parse(toBeChanged.Id), accessPackage.Urn!, cancellationToken);

                    if (delegationResult.IsProblem)
                    {
                        return new Result<bool>(delegationResult.Problem!);
                    }
                }
            }
        }

        // Delegate new Single Rights to the SystemUser
        if (delegationCheckFinalResult.CanDelegate && delegationCheckFinalResult.RightResponses?.Count > 0)
        {
            Result<bool> delegationSucceeded = await accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), toBeChanged, delegationCheckFinalResult.RightResponses);
            if (delegationSucceeded.IsProblem)
            {
                return delegationSucceeded.Problem;
            }
        }

        // Attempt to Revoke Single Rights from the SystemUser
        if (systemUserChangeRequest.UnwantedRights?.Count > 0)
        {
            var revokeRightResult = await accessManagementClient.RevokeDelegatedRightToSystemUser(partyId.ToString(), toBeChanged, systemUserChangeRequest.UnwantedRights);
            if (revokeRightResult.IsProblem)
            {
                return revokeRightResult.Problem;
            }
        }

        // Attempt to Revoke AccessPackages from the SystemUser
        if (verifiedUnwantedAccessPackages.Value?.Count > 0)
        {
            foreach (AccessPackage accessPackage in verifiedUnwantedAccessPackages.Value)
            {
                var removeSystemUserResult = await accessManagementClient.DeleteSingleAccessPackageFromSystemUser(partyUuid, new Guid(toBeChanged.Id), accessPackage.Urn, cancellationToken);
                if (removeSystemUserResult.IsProblem)
                {
                    return removeSystemUserResult.Problem;
                }
            }
        }

        // Persist Approval
        var changed = await changeRequestRepository.PersistApprovalOfChangeRequest(requestId, toBeChanged, userId, cancellationToken);

        if (!changed)
        {
            return Problem.RequestCouldNotBeUpdated;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
    {
        ChangeRequestResponse? systemUserRequest = await changeRequestRepository.GetChangeRequestById(requestId);
        if (systemUserRequest is null)
        {
            return Problem.RequestNotFound;
        }

        if (systemUserRequest.Status != RequestStatus.New.ToString())
        {
            return Problem.RequestStatusNotNew;
        }

        return await changeRequestRepository.RejectChangeOnSystemUser(requestId, userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<Page<ChangeRequestResponse, Guid>>> GetAllChangeRequestsForVendor(
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

        List<ChangeRequestResponse>? theList = await changeRequestRepository.GetAllChangeRequestsBySystem(systemId, cancellationToken);
        theList ??= [];

        return Page.Create(theList, _paginationSize, static theList => theList.Id);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeleteChangeRequestByRequestId(Guid requestId)
    {
        var result = await changeRequestRepository.DeleteChangeRequestByRequestId(requestId);
        if (result)
        {
            return true;
        }

        return Problem.RequestNotFound;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetRedirectByChangeRequestId(Guid requestId)
    {
        ChangeRequestResponse? systemUserRequest = await changeRequestRepository.GetChangeRequestById(requestId);
        if (systemUserRequest is null || systemUserRequest.RedirectUrl is null)
        {
            return Problem.RequestNotFound;
        }

        return systemUserRequest.RedirectUrl;
    }

    private async Task<Result<RegisteredSystemResponse>> ValidateChangeRequest(ChangeRequestResponse validateSet, OrganisationNumber vendorOrgNo, bool createNew)
    {   
        Result<bool> valRef = await ValidateStatus(validateSet.Id, createNew);
        if (valRef.IsProblem)
        {
            return valRef.Problem;
        }

        RegisteredSystemResponse? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(validateSet.SystemId);
        if (systemInfo is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<bool> valVendor = ValidateVendorOrgNo(vendorOrgNo, systemInfo);
        if (valVendor.IsProblem)
        {
            return valVendor.Problem;
        }

        Result<bool> valCust = await ValidateCustomerOrgNo(validateSet.PartyOrgNo);
        if (valCust.IsProblem)
        {
            return valCust.Problem;
        }

        if (validateSet.RedirectUrl is not null && validateSet.RedirectUrl != string.Empty)
        {
            var valRedirect = AuthenticationHelper.ValidateRedirectUrl(validateSet.RedirectUrl, systemInfo.AllowedRedirectUrls);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }

        if (validateSet.RequiredRights is not null && validateSet.RequiredRights.Count > 0)
        {
            Result<bool> valRights = systemUserService.ValidateRights(validateSet.RequiredRights, systemInfo);
            if (valRights.IsProblem)
            {
                return valRights.Problem;
            }
        }

        if (validateSet.RequiredAccessPackages is not null && validateSet.RequiredAccessPackages.Count > 0)
        {
            Result<bool> valPackages = await systemUserService.ValidateAccessPackages(validateSet.RequiredAccessPackages, systemInfo, isAgentRequest: false);
            if (valPackages.IsProblem)
            {
                return valPackages.Problem;
            }
        }

        return systemInfo;
    }

    /// <inheritdoc/>
    public async Task<Result<ChangeRequestResponse>> VerifySetOfRights(ChangeRequestResponse verifyRequest, SystemUserInternalDTO systemUser, OrganisationNumber vendorOrgNo)
    {
        Result<RegisteredSystemResponse> valSet = await ValidateChangeRequest(verifyRequest, vendorOrgNo, createNew: false);
        if (valSet.IsProblem)
        {
            return valSet.Problem;
        }

        ChangeRequestStatus changeRequestStatus = ChangeRequestStatus.NoChangeNeeded;

        Party party = await partiesClient.GetPartyByOrgNo(systemUser.ReporteeOrgNo);

        if (party is null || party.PartyUuid is null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        Guid partyUuid = (Guid)party.PartyUuid;

        Result<List<AccessPackage>> verifiedRequiredAccessPackages = await VerifyAccessPackages(verifyRequest.RequiredAccessPackages, partyUuid, systemUser, true);
        if (verifiedRequiredAccessPackages.IsProblem)
        {
            return verifiedRequiredAccessPackages.Problem;
        }

        if (verifiedRequiredAccessPackages.Value.Count > 0)
        {
            changeRequestStatus = ChangeRequestStatus.New;
        }

        Result<List<AccessPackage>> verifiedUnwantedAccessPackages = await VerifyAccessPackages(verifyRequest.UnwantedAccessPackages, partyUuid, systemUser, false);

        if (verifiedUnwantedAccessPackages.IsProblem)
        {
            return verifiedUnwantedAccessPackages.Problem;
        }

        return new ChangeRequestResponse()
        {
            Id = Guid.NewGuid(),
            ExternalRef = verifyRequest.ExternalRef, 
            SystemId = verifyRequest.SystemId,
            SystemUserId = Guid.Parse(systemUser.Id),
            PartyOrgNo = verifyRequest.PartyOrgNo,
            RequiredRights = verifyRequest.RequiredRights,
            UnwantedRights = verifyRequest.UnwantedRights,
            RequiredAccessPackages = verifiedRequiredAccessPackages.Value,
            UnwantedAccessPackages = verifiedUnwantedAccessPackages.Value,
            Status = ChangeRequestStatus.New.ToString(),
            RedirectUrl = verifyRequest.RedirectUrl
        };
    }

    /// <summary>
    /// Find the currently delegated AccessPackages to a SystemUser
    /// </summary>
    /// <param name="accessPackages">AccessPackeges</param>
    /// <param name="partyUuid">The Uuid for the reportee owning the Systemuser</param>
    /// <param name="systemUser">The SystemUser</param>
    /// <param name="required">Whether all should be required, or all are unwanted</param>
    /// <param name="cancellationToken">cancellationToken </param>
    /// <returns>List of difference, an empty list means all are as required</returns>
    private async Task<Result<List<AccessPackage>>> VerifyAccessPackages(List<AccessPackage> accessPackages, Guid partyUuid, SystemUserInternalDTO systemUser, bool required, CancellationToken cancellationToken = default)
    {
        // The result is stored here, we are looking for the difference between what is required and what is current
        List<AccessPackage> diff = [];
        Result<List<AccessPackage>> currentAccessPackages = await delegationHelper.GetAccessPackagesForSystemUser(partyUuid, new Guid(systemUser.Id), cancellationToken);
        if (currentAccessPackages.IsProblem) 
        { 
            return currentAccessPackages.Problem; 
        }

        foreach (AccessPackage accessPackage in accessPackages)
        {
            // Look for required ap that are not currently delegated
            if (required && !currentAccessPackages.Value.Any(u => u.Urn == accessPackage.Urn))
            {
                diff.Add(accessPackage);
            }

            // Look for unwanted ap that are currently delegated
            if (!required && currentAccessPackages.Value.Any(u => u.Urn == accessPackage.Urn))
            {
                diff.Add(accessPackage);
            }
        }

        // The difference found
        return diff;
    }

    /// <summary>
    /// Verifies the entire set of single rights with the PDP as a complete set,
    /// does not identify which were missing if the result is not PERMIT
    /// </summary>
    /// <returns>true or false</returns>
    private async Task<Result<List<Right>>> VerifySingleRightsWithPDP(List<Right> rights, SystemUserInternalDTO systemUser, bool required)
    {        
        List<PolicyRightsDTO> requiredPolicyRights = [];

        // Need this since the Result type cant init from [] directly
        List<Right> empty = [];

        // Call the Resource Registry to get a flat list of Rights in the PDP format from the list of ResourceIds.
        foreach (var right in rights)
        {
            foreach (var resource in right.Resource)
            {
                var flatPolicyRight = await resourceRegistryClient.GetRights(resource.Value);
                var filteredRights = FilterFlatPolicyRight(flatPolicyRight);
                requiredPolicyRights.AddRange(filteredRights);
            }
        }

        // Call the PDP client to verify that the entire set of Rights in the list are currently delegated.        
        var res = await MultipleDecisionRequestToPDP(requiredPolicyRights, systemUser);
        if (res.IsProblem)
        {
            return res.Problem;
        }

        // In the case of Required Rights 
        if (required)
        {
            // All the Required Rights are Permit allready, no change needed, return empty list
            if (MapPDPResponseAllPermit(res.Value))
            {
                return empty;
            }
        }
        else
        {
            // In the case of Unwanted Rights, no Right has Permit, no change needed, return empty list
            if (MapPDPResponseNonePermit(res.Value))
            {
                return empty;
            }            
        }

        // A change is needed, return the list of Rights
        return rights;
    }

    /// <summary>
    /// Removes the rights which only have "urn:altinn:org" as a subject type.
    /// </summary>
    /// <param name="flatPolicyRight">list</param>
    /// <returns>filtered list of PolicyRightsDTO</returns>
    private static List<PolicyRightsDTO> FilterFlatPolicyRight(List<PolicyRightsDTO> flatPolicyRight)
    {
        List<PolicyRightsDTO> filteredList = [];

        foreach (PolicyRightsDTO right in flatPolicyRight)
        {
            bool hasOtherSubjectTypeThanOrg = false;
            foreach (string subjectType in right.SubjectTypes)
            {
                if (subjectType != "urn:altinn:org")
                {
                    hasOtherSubjectTypeThanOrg = true;
                    break;
                }
            }

            if (hasOtherSubjectTypeThanOrg)
            {
                filteredList.Add(right);
            }
        }

        return flatPolicyRight;
    }

    /// <summary>
    /// Returns True if all the Rights in the PDP call were Permit,
    /// a False will be returned if any of the Rights were not Permit.
    /// This means that the ChangeRequest should be submitted to the API, 
    /// since it is not already delegated, and the PDP API is idempotent,
    /// it does not matter if one or more of the Rights are already delegated.
    /// </summary>
    /// <param name="res">The response from the PDP</param>
    /// <returns>Boolean True if all Rights where Permit</returns>    
    private static bool MapPDPResponseAllPermit(XacmlJsonResponse res)
    {
        bool allRequiredRightsAreDelegated = true;

        foreach (XacmlJsonResult result in res.Response) 
        {
            if (result.Decision != XacmlContextDecision.Permit.ToString())
            {
                allRequiredRightsAreDelegated = false;
            }           
        }

        return allRequiredRightsAreDelegated;
    }

    /// <summary>
    /// Returns True if none of the Rights in the PDP call were Permit,
    /// a False will be returned if any of the Rights were Permit.
    /// </summary>
    /// <param name="res">The response from the PDP</param>
    /// <returns>Boolean True if no Rights where Permit</returns>    
    private static bool MapPDPResponseNonePermit(XacmlJsonResponse res)
    {
        bool noPermit = true;

        foreach (XacmlJsonResult result in res.Response)
        {
            if (result.Decision == XacmlContextDecision.Permit.ToString())
            {
                noPermit = false;
            }
        }

        return noPermit;
    }

    private async Task<Result<XacmlJsonResponse>> MultipleDecisionRequestToPDP(List<PolicyRightsDTO> rights, SystemUserInternalDTO systemUser)
    {
        XacmlJsonCategory xacmlUser = new()
        {
            Id = "s1",
            Attribute = 
            [
                new XacmlJsonAttribute
                {
                    AttributeId = "urn:altinn:systemuser:uuid",
                    Value = systemUser.Id
                }
            ] 
        };

        List<XacmlJsonCategory> accessSubject = [xacmlUser];

        List<XacmlJsonRequestReference> multiRequests = [];

        List<XacmlJsonCategory> actionList = [];

        List<XacmlJsonCategory> resourceList = [];

        int counter = 0;
        foreach (PolicyRightsDTO right in rights) 
        {
            counter++;
            XacmlJsonCategory xamlAction = new()
            {
                Id = $"a{counter}",
                Attribute =
                [
                    new XacmlJsonAttribute
                    {
                        AttributeId = "urn:oasis:names:tc:xacml:1.0:action:action-id",
                        Value = right.Action.Value.ValueSpan.ToString()
                    }
                ]
            };

            actionList.Add(xamlAction);
                        
            List<XacmlJsonAttribute> resourceAttributes = [];

            foreach (var res in right.Resource)
            {
                var newres = new XacmlJsonAttribute
                {
                    AttributeId = "urn:altinn:resource",
                    Value = res.Value.ValueSpan.ToString()
                };

                resourceAttributes.Add(newres); 
            }

            // Add the resource owner, that in this case will be the partyId for the user
            XacmlJsonAttribute resourcePartyAttribute = new()
            {
                AttributeId = "urn:altinn:partyid",
                Value = systemUser.PartyId
            };

            resourceAttributes.Add(resourcePartyAttribute);

            XacmlJsonCategory xamlResource = new()
            {
                Id = $"r{counter}",
                Attribute = resourceAttributes
            };

            resourceList.Add(xamlResource);

            var reqref = new XacmlJsonRequestReference
            {
                ReferenceId = [xacmlUser.Id, xamlAction.Id, xamlResource.Id]
            };

            multiRequests.Add(reqref);            
        }

        XacmlJsonRequestRoot request = new()
        {
            Request = new XacmlJsonRequest
            {
                ReturnPolicyIdList = true,
                AccessSubject = accessSubject,
                Action = actionList,
                Resource = resourceList,   

                MultiRequests = new XacmlJsonMultiRequests()
                {
                    RequestReference = multiRequests
                }
            }
        };

        return await PDPClient.GetDecisionForRequest(request);
    }

    /// <inheritdoc/>
    public async Task<Result<ChangeRequestResponseInternal>> CheckUserAuthorizationAndGetRequest(Guid requestId)
    {
        ChangeRequestResponse? req = await changeRequestRepository.GetChangeRequestById(requestId);
        if (req == null)
        {
            return Problem.RequestNotFound;
        }

        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return Problem.RequestNotFound;
        }

        IEnumerable<Claim> claims = context.User.Claims;

        Party party = await partiesClient.GetPartyByOrgNo(req.PartyOrgNo);

        if (!party.PartyUuid.HasValue)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        Guid partyUuid = (Guid)party.PartyUuid;

        XacmlJsonRequestRoot jsonRequest = SpecificDecisionHelper.CreateDecisionRequestForUserId(claims, "write", "altinn_access_management", partyUuid);

        XacmlJsonResponse response = await PDPClient.GetDecisionForRequest(jsonRequest);
        if (response is null)
        {
            return Problem.RequestNotFound;
        }

        List<AccessPackage> requiredAccessPackages = [];

        if (req.RequiredAccessPackages?.Count > 0)
        {
            Result<AccessPackageDelegationCheckResult> checkAccessPackages = await delegationHelper.ValidateDelegationRightsForAccessPackages(partyUuid, req.SystemId, req.RequiredAccessPackages, fromBff: false);
            if (checkAccessPackages.IsProblem)
            {
                return checkAccessPackages.Problem;
            }

            if (checkAccessPackages.IsSuccess && checkAccessPackages.Value?.AccessPackages?.Count > 0) 
            {
                requiredAccessPackages = checkAccessPackages.Value.AccessPackages;
            }
        }

        if (SpecificDecisionHelper.ValidatePdpDecision(response, context.User))
        {
            return new ChangeRequestResponseInternal()
            {
                Id = requestId,
                ExternalRef = req.ExternalRef,
                SystemId = req.SystemId,
                PartyOrgNo = req.PartyOrgNo,
                PartyId = party.PartyId,
                PartyUuid = partyUuid,
                RequiredRights = req.RequiredRights,
                UnwantedRights = req.UnwantedRights,
                RequiredAccessPackages = requiredAccessPackages,
                UnwantedAccessPackages = req.UnwantedAccessPackages,
                Status = req.Status,
                ConfirmUrl = req.ConfirmUrl,
                Created = req.Created,
                RedirectUrl = req.RedirectUrl,
                SystemUserId = req.SystemUserId
            };
        }

        return Problem.RequestNotFound;        
    }
}
