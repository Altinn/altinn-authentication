namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamFrontChannelLogoutResult
    {
        public int TerminatedSessions { get; init; }

        public IReadOnlyList<CookieInstruction> Cookies { get; init; } = Array.Empty<CookieInstruction>();
    }
}
