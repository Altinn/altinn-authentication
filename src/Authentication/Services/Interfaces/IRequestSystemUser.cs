using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Services.Interfaces;

/// <summary>
/// The Service that support the CRUD API for administration of SystemUser-Requests
/// </summary>
public interface IRequestSystemUser
{
    /// <summary>
    /// Create a new Request for a SystemUser
    /// 
    /// </summary>
    /// <param name="createRequest">The model describing a new Request for a SystemUser</param>
    /// <returns>Response model</returns>
    Task<CreateRequestSystemUserResponse> CreateRequest(CreateRequestSystemUser createRequest);

    /// <summary>
    /// Gets the status based on both externalref and systemid
    /// 
    /// </summary>
    /// <param name="systemId">The system</param>
    /// <param name="externalRef1">The Vendor's chosen external ref ( or orgno by default) </param>
    /// <returns>The Status Response model</returns>
    Task<CreateRequestSystemUserResponse> GetRequestByExternalRef(string systemId, string externalRef1);

    /// <summary>
    /// Get the status by UUID Request Id
    /// 
    /// </summary>
    /// <param name="requestId">The Request Id as a UUID</param>
    /// <returns>The Status Response model</returns>
    Task<CreateRequestSystemUserResponse> GetRequestByGuid(Guid requestId);
}
