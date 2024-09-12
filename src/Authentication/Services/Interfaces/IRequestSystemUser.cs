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
    /// </summary>
    /// <param name="createRequest">The model describing a new Request for a SystemUser</param>
    /// <returns>Response model</returns>
    Task<CreateRequestSystemUserResponse> CreateRequest(CreateRequestSystemUser createRequest);
}
