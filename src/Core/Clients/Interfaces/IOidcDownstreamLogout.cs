using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Clients.Interfaces
{
    public interface IOidcDownstreamLogout
    {
        Task<bool> TryLogout(OidcClient oidcClient, string sessionId, string iss, CancellationToken cancellationToken);
    }
}
