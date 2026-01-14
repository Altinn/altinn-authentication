namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class EndSessionResult
    {
        public Uri? RedirectUri { get; init; }
        public string? State { get; init; }
        public IReadOnlyList<CookieInstruction> Cookies { get; init; } = Array.Empty<CookieInstruction>();
    }
}
