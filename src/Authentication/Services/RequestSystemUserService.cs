using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services;

/// <inheritdoc/>
public class RequestSystemUserService : IRequestSystemUser
{
    /// <inheritdoc/>
    public async Task<CreateRequestSystemUserResponse> CreateRequest(CreateRequestSystemUser createRequest)
    {
        await Task.Delay(65);

        return new CreateRequestSystemUserResponse(
            createRequest.Id,
            createRequest.ExternalRef,
            createRequest.SystemId,
            createRequest.PartyOrgNo,
            createRequest.Rights,
            RequestStatus.New.ToString(),
            createRequest.RedirectURL,
            Guid.NewGuid().ToString());
    }
}
