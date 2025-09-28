using Altinn.Platform.Authentication.Core.Models.Oidc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface ITokenService
    {
        Task<TokenResult> ExchangeAuthorizationCodeAsync(TokenRequest request, CancellationToken ct);
    }
}
