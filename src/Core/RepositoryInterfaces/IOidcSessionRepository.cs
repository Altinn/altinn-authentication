using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IOidcSessionRepository
    {
        /// <summary>Create a new session or refresh an existing matching one (by Provider+UpstreamSub), returning the current SID.</summary>
        Task<OidcSession> UpsertByUpstreamSubAsync(OidcSessionCreate create, CancellationToken ct = default);

        /// <summary>Load by SID (for logout / introspection).</summary>
        Task<OidcSession?> GetBySidAsync(string sid, CancellationToken ct = default);

        /// <summary>
        /// Load by session handle 
        /// </summary>
        Task<OidcSession?> GetBySessionHandleAsync(byte[] sessionHandle, CancellationToken ct = default);

        /// <summary>Invalidate by SID (on logout).</summary>
        Task<bool> DeleteBySidAsync(string sid, CancellationToken ct = default);

        /// <summary>
        /// Touch the LastSeen timestamp of the session to now (for sliding expiration).
        /// </summary>
        Task TouchLastSeenAsync(string sid, CancellationToken ct = default);

        /// <summary>
        /// Slide the expiry of the session to a new value, if and only if the new value is later than the current expiry.
        /// </summary>
        Task<bool> SlideExpiryToAsync(string sid, DateTimeOffset newExpiresAt, CancellationToken ct = default);
        Task<string[]> GetSidsByUpstreamAsync(string issuer, string upstreamSid, CancellationToken ct);
        Task<int> DeleteByUpstreamAsync(string issuer, string upstreamSid, CancellationToken ct);
    }
}
