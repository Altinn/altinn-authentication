using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface ITokenService
    {
        Task<TokenResult> ExchangeAuthorizationCodeAsync(TokenRequest request, CancellationToken ct);
    }
}
