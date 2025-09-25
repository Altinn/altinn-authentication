using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IOidcServerRepository
    {
        Task<OidcClient?> GetClientAsync(string clientId, CancellationToken ct = default);
    }
}
