namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamCallbackDto
    {
        public string? Code { get; init; }
        public string? State { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }
        public string? Iss { get; init; } // optional routing hint if you include ?iss=... in redirect_uri
    }
}
