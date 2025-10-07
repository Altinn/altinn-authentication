using System.Security.Claims;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamFrontChannelLogoutInput
    {
        public string Issuer { get; init; } = null!;
        public string UpstreamSid { get; init; } = null!;
        public ClaimsPrincipal? User { get; init; }
    }
}
