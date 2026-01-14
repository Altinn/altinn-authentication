using System.Net;
using System.Security.Claims;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class EndSessionInput
    {
        public string? IdTokenHint { get; init; }
        public Uri? PostLogoutRedirectUri { get; init; }
        public string? State { get; init; }
        public ClaimsPrincipal? User { get; init; }
        public IPAddress? ClientIp { get; init; }
        public string? UserAgentHash { get; init; }
    }
}
