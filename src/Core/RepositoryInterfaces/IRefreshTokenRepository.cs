using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IRefreshTokenRepository
    {
        Task<Guid> GetOrCreateFamilyAsync(string clientId, string subjectId, string opSid, CancellationToken ct);
        Task InsertAsync(RefreshTokenRow row, CancellationToken ct);
        Task<RefreshTokenRow?> GetByLookupKeyAsync(byte[] lookupKey, CancellationToken ct);
        Task MarkUsedAsync(Guid tokenId, Guid rotatedToTokenId, CancellationToken ct);
        Task RevokeAsync(Guid tokenId, string reason, CancellationToken ct);
        Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct);
        Task<IReadOnlyList<Guid>> GetFamiliesByOpSidAsync(string opSid, CancellationToken ct);
    }
}
