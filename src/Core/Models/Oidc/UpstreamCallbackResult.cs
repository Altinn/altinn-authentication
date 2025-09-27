namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamCallbackResult
    {
        public UpstreamCallbackResultKind Kind { get; init; }

        // Success
        public Uri? ClientRedirectUri { get; init; }
        public string? DownstreamCode { get; init; }
        public string? ClientState { get; init; }

        // Error redirect
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }

        // Local error
        public int? StatusCode { get; init; }
        public string? LocalErrorMessage { get; init; }

        public IEnumerable<CookieInstruction> Cookies { get; init; } = Array.Empty<CookieInstruction>();
    }
}
