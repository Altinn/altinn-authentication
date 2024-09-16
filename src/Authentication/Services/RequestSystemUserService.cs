using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services;

/// <inheritdoc/>
public class RequestSystemUserService
    : IRequestSystemUser
{
    private readonly Dictionary<ExternalRequestId, CreateRequestSystemUserResponse> _mockList = [];

    /// <inheritdoc/>
    public async Task<Result<CreateRequestSystemUserResponse>> CreateRequest(CreateRequestSystemUser createRequest, string vendorOrgNo)
    {
        // The combination of SystemId + Customer's OrgNo and Vendor's External Reference must be unique, for both all Requests and SystemUsers.
        ExternalRequestId externalRequestId = new()
        {
            ExternalRef = createRequest.ExternalRef,
            OrgNo = createRequest.PartyOrgNo,
            SystemId = createRequest.SystemId,
        };
                
        var valRef = await ValidateExternalRequestId(externalRequestId);
        if (valRef.IsProblem)
        {
            return valRef.Problem;
        }
                
        var valVendor = await ValidateVendorOrgNo(vendorOrgNo, createRequest.SystemId);      
        if (valVendor.IsProblem)
        {
            return valVendor.Problem;
        }
                
        var valCust = await ValidateCustomerOrgNo(createRequest.PartyOrgNo);
        if (valCust.IsProblem)
        {
            return valCust.Problem;
        }
        
        var valRedirect = await ValidateRedirectUrl(createRequest.RedirectUrl);
        if (valRedirect.IsProblem)
        {
            return valRedirect.Problem;
        }

        var valRights = await ValidateRights(createRequest.Rights);
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

        var created = new CreateRequestSystemUserResponse()
        {
            Id = newId,
            ExternalRef = createRequest.ExternalRef,
            SystemId = createRequest.SystemId,
            PartyOrgNo = createRequest.PartyOrgNo,
            Rights = createRequest.Rights,
            Status = RequestStatus.New.ToString(),
            RedirectUrl = createRequest.RedirectUrl,
            SystemUserId = newId
        };

        _mockList.Add(externalRequestId, created);

        return created;
    }

    /// <summary>
    /// Validate that the Rights is both a subset of the Default Rights registered on the System, and at least one Right is selected
    /// </summary>
    /// <param name="rights">the Rights chosen for the Request</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateRights(List<Right> rights)
    {
        return true;
    }

    /// <summary>
    /// Validate that the RedirectUrl chosen is the same as one of the RedirectUrl's listed for the Registered System
    /// </summary>
    /// <param name="redirectURL">the RedirectUrl chosen</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateRedirectUrl(string redirectURL)
    {
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
        var res = _mockList[externalRequestId];

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
    /// <param name="systemId">the chosen SystemId </param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateVendorOrgNo(string vendorOrgNo, string systemId)
    {
        return true;
    }

    /// <summary>
    /// Validate the PartyOrgNo for the Customer towards ER
    /// </summary>
    /// <param name="partyOrgNo">the PartyOrgNo for the Customer</param>
    /// <returns>Result or Problem</returns>
    private async Task<Result<bool>> ValidateCustomerOrgNo(string partyOrgNo)
    {
        return true;
    }

    /// <inheritdoc/>
    public async Task<CreateRequestSystemUserResponse> GetRequestByExternalRef(ExternalRequestId externalRequestId)
    {
        var id = Guid.NewGuid();

        return new CreateRequestSystemUserResponse()
        {
            Id = id,
            ExternalRef = externalRequestId.ExternalRef,
            SystemId = externalRequestId.SystemId,
            PartyOrgNo = externalRequestId.OrgNo,
            Rights = [],
            Status = RequestStatus.New.ToString(),
            RedirectUrl = null,
            SystemUserId = id
        };
    }

    /// <inheritdoc/>
    public async Task<CreateRequestSystemUserResponse> GetRequestByGuid(Guid requestId)
    {
        return new CreateRequestSystemUserResponse()
        {
            Id = requestId,
            ExternalRef = "1234567",
            SystemId = "1234567_cool_system",
            PartyOrgNo = "1234567",
            Rights = [],
            Status = RequestStatus.New.ToString(),
            RedirectUrl = null,
            SystemUserId = requestId
        };
    }

    /// <inheritdoc/>
    public async Task<CreateRequestSystemUserResponse> GetRequestByExternalRef(string systemId, string externalRef1)
    {
        await Task.Delay(65);

        return new CreateRequestSystemUserResponse()
        {
            Id = Guid.NewGuid(),
            ExternalRef = externalRef1,
            SystemId = systemId,
            PartyOrgNo = "1234567",
            Rights = [],
            Status = RequestStatus.New.ToString(),
            RedirectURL = null,
            SystemUserId = Guid.NewGuid().ToString()
        };
    }

    /// <inheritdoc/>
    public async Task<CreateRequestSystemUserResponse> GetRequestByGuid(Guid requestId)
    {
        await Task.Delay(65);

        return new CreateRequestSystemUserResponse()
        {
            Id = requestId,
            ExternalRef = "1234567",
            SystemId = "1234567_cool_system",
            PartyOrgNo = "1234567",
            Rights = [],
            Status = RequestStatus.New.ToString(),
            RedirectURL = null,
            SystemUserId = Guid.NewGuid().ToString()
        };
    }
}
