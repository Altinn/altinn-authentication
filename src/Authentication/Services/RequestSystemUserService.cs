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

        return new CreateRequestSystemUserResponse()
        {
            Id = Guid.NewGuid(),
            ExternalRef = createRequest.ExternalRef,
            SystemId = createRequest.SystemId,
            PartyOrgNo = createRequest.PartyOrgNo,
            Rights = createRequest.Rights,
            Status = RequestStatus.New.ToString(),
            RedirectURL = createRequest.RedirectURL,
            SystemUserId = Guid.NewGuid().ToString()
        };
    }
}
