using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IOidcSessionRepository
    {
        /// <summary>Create a new session or refresh an existing matching one (by Provider+UpstreamSub), returning the current SID.</summary>
        Task<OidcSession> UpsertByUpstreamSubAsync(OidcSessionCreate create, CancellationToken ct = default);

        /// <summary>Load by SID (for logout / introspection).</summary>
        Task<OidcSession?> GetBySidAsync(string sid, CancellationToken ct = default);

        /// <summary>Invalidate by SID (on logout).</summary>
        Task<bool> DeleteBySidAsync(string sid, CancellationToken ct = default);
    }
}
