using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IAuthorizationCodeRepository
    {
        Task InsertAsync(AuthorizationCodeCreate create, CancellationToken ct = default);

        Task<AuthCodeRow?> GetAsync(string code, CancellationToken ct = default);

        Task ConsumeAsync(string code, DateTimeOffset usedAt, CancellationToken ct = default);
    }
}
