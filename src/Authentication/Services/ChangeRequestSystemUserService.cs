﻿using System;
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
using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
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
    public async Task<Result<ChangeRequestResponse>> CreateChangeRequest(ChangeRequestSystemUser createRequest, OrganisationNumber vendorOrgNo)
    {
        Result<ChangeRequestValidationSet> validationSet = await ValidateChangeRequest(createRequest, vendorOrgNo);
        if (validationSet.IsProblem)
        {
            return validationSet.Problem;
        }

        Result<ChangeRequestResponse> verified = await VerifySetOfRights(createRequest, vendorOrgNo);
        if (verified.IsProblem)
        {
            return verified.Problem;
        }

        // Similar as to running the Verify endpoint, but we need to check here too, since the vendor might not have verified the request first.
        if (verified.Value.Status == ChangeRequestStatus.NoChangeNeeded.ToString())
        {
            return verified.Value;
        }

        if (createRequest.RedirectUrl is not null && createRequest.RedirectUrl != string.Empty)
        {
            var valRedirect = ValidateRedirectUrl(createRequest.RedirectUrl, validationSet.Value.RegisteredSystem);
            if (valRedirect.IsProblem)
            {
                return valRedirect.Problem;
            }
        }     

        Guid newId = Guid.NewGuid();

        var created = new ChangeRequestResponse()
        {
            Id = newId,
            SystemUserId = Guid.Parse(validationSet.Value.SystemUser.Id),
            ExternalRef = createRequest.ExternalRef,
            SystemId = createRequest.SystemId,
            PartyOrgNo = createRequest.PartyOrgNo,
            RequiredRights = createRequest.RequiredRights,
            UnwantedRights = createRequest.UnwantedRights,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createRequest.RedirectUrl
        };

        Result<bool> res = await changeRequestRepository.CreateChangeRequest(created);
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
    /// <param name="systemInfo">the System</param>
    /// <returns>Result or Problem</returns>
    private static Result<bool> ValidateRights(List<Right> rights, RegisteredSystemResponse systemInfo)
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
                return Problem.Rights_NotFound_Or_NotDelegable;
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
    /// <param name="externalRequestId">Combination of SystemId, PartyOrg and External Ref</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateExternalChangeRequestId(ExternalRequestId externalRequestId)
    {
        ChangeRequestResponse? res = await changeRequestRepository.GetChangeRequestByExternalReferences(externalRequestId);

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
        ChangeRequestResponse? res = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
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

        ChangeRequestResponse? find = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
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
            Status = find.Status,
            RedirectUrl = find.RedirectUrl,
            SystemUserId = find.SystemUserId
        };

        return request;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAndDelegateChangeOnSystemUser(Guid requestId, int partyId, int userId, CancellationToken cancellationToken)
    {
        ChangeRequestResponse? systemUserChangeRequest = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
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

        SystemUser? toBeChanged = await systemUserService.GetSingleSystemUserById(systemUserChangeRequest.SystemUserId);
        if (toBeChanged is null)
        {
            return Problem.SystemUserNotFound;
        }

        DelegationCheckResult delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(partyId, regSystem.Id, systemUserChangeRequest.RequiredRights, false, cancellationToken);
        if (!delegationCheckFinalResult.CanDelegate || delegationCheckFinalResult.RightResponses is null)
        {
            return Problem.Rights_NotFound_Or_NotDelegable;
        }

        var changed = await changeRequestRepository.ApproveAndDelegateOnSystemUser(requestId, toBeChanged, userId, cancellationToken);

        if (!changed)
        {
            return Problem.SystemUser_FailedToCreate;
        }

        Result<bool> delegationSucceeded = await accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), toBeChanged, delegationCheckFinalResult.RightResponses);
        if (delegationSucceeded.IsProblem)
        {
            return delegationSucceeded.Problem;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
    {
        ChangeRequestResponse? systemUserRequest = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
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
        ChangeRequestResponse? systemUserRequest = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
        if (systemUserRequest is null || systemUserRequest.RedirectUrl is null)
        {
            return Problem.RequestNotFound;
        }

        return systemUserRequest.RedirectUrl;
    }

    private async Task<Result<ChangeRequestValidationSet>> ValidateChangeRequest(ChangeRequestSystemUser validateSet, OrganisationNumber vendorOrgNo)
    {
        // Set an empty ExternalRef to be equal to the PartyOrgNo
        if (validateSet.ExternalRef is null || validateSet.ExternalRef == string.Empty)
        {
            validateSet.ExternalRef = validateSet.PartyOrgNo;
        }

        // The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers.
        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = validateSet.ExternalRef ?? validateSet.PartyOrgNo,
            OrgNo = validateSet.PartyOrgNo,
            SystemId = validateSet.SystemId,
        };

        SystemUser? systemUser = await systemUserService.GetSystemUserByExternalRequestId(externalRequestId);
        if (systemUser is null)
        {
            return Problem.SystemUserNotFound;
        }

        RegisteredSystemResponse? systemInfo = await systemRegisterService.GetRegisteredSystemInfo(validateSet.SystemId);
        if (systemInfo is null)
        {
            return Problem.SystemIdNotFound;
        }

        Result<bool> valRights = ValidateRights(validateSet.RequiredRights, systemInfo);
        if (valRights.IsProblem)
        {
            return valRights.Problem;
        }

        Result<bool> valRef = await ValidateExternalChangeRequestId(externalRequestId);
        if (valRef.IsProblem)
        {
            return valRef.Problem;
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

        return new ChangeRequestValidationSet()
        {
            ExternalRequestId = externalRequestId,
            SystemUser = systemUser,
            RegisteredSystem = systemInfo
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ChangeRequestResponse>> VerifySetOfRights(ChangeRequestSystemUser verifyRequest, OrganisationNumber vendorOrgNo)
    {
        Result<ChangeRequestValidationSet> valSet = await ValidateChangeRequest(verifyRequest, vendorOrgNo);
        if (valSet.IsProblem)
        {
            return valSet.Problem;
        }

        List<PolicyRightsDTO> requiredPolicyRights = [];

        // Call the Resource Registry to get a flat list of Rights in the PDP format from the list of ResourceIds.
        foreach (var right in verifyRequest.RequiredRights)
        {
            foreach (var resource in right.Resource)
            {
                var flatPolicyRight = await resourceRegistryClient.GetRights(resource.Value);
                var filteredRights = FilterFlatPolicyRight(flatPolicyRight);
                requiredPolicyRights.AddRange(filteredRights);
            }
        }

        // Call the PDP client to verify which of the Required Rights are currently delegated.
        // The Unwanted rights verification is currently not supported.
        var res = await MultipleDecisionRequestToPDP(requiredPolicyRights, valSet.Value.SystemUser);
        if (res.IsProblem) 
        {
            return res.Problem;
        }

        bool allRequiredRightsAreDelegated = MapPDPResponse(res.Value);

        if (allRequiredRightsAreDelegated)
        {
            return new ChangeRequestResponse()
            {
                Id = Guid.NewGuid(),
                ExternalRef = verifyRequest.ExternalRef,
                SystemId = verifyRequest.SystemId,
                PartyOrgNo = verifyRequest.PartyOrgNo,
                RequiredRights = [],
                UnwantedRights = verifyRequest.UnwantedRights,
                Status = ChangeRequestStatus.NoChangeNeeded.ToString(),
                RedirectUrl = verifyRequest.RedirectUrl
            };
        }

        // The Rights are not all delegated, so we need to create a new ChangeRequest
        return new ChangeRequestResponse()
        {
            Id = Guid.NewGuid(),
            ExternalRef = JsonSerializer.Serialize(res.Value),
            SystemId = verifyRequest.SystemId,
            SystemUserId = Guid.Parse(valSet.Value.SystemUser.Id),
            PartyOrgNo = verifyRequest.PartyOrgNo,
            RequiredRights = verifyRequest.RequiredRights,
            UnwantedRights = verifyRequest.UnwantedRights,
            Status = ChangeRequestStatus.New.ToString(),
            RedirectUrl = verifyRequest.RedirectUrl
        };
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
    /// Returns True if all the Rights in the PDP call where Permit,
    /// a False will be returned if any of the Rights where not Permit.
    /// This means that the ChangeRequest should be submitted to the API, 
    /// since it is not already delegated, and the PDP API is idempotent,
    /// it does not matter if one or more of the Rights are already delegated.
    /// </summary>
    /// <param name="res">The response from the PDP</param>
    /// <returns>Boolean True if all Rights where Permit</returns>    
    private static bool MapPDPResponse(XacmlJsonResponse res)
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

    private async Task<Result<XacmlJsonResponse>> MultipleDecisionRequestToPDP(List<PolicyRightsDTO> rights, SystemUser systemUser)
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
        ChangeRequestResponse? req = await changeRequestRepository.GetChangeRequestByInternalId(requestId);
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
