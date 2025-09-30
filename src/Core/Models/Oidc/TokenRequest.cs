namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class TokenRequest
    {
        public required string GrantType { get; init; }                 // "authorization_code"
        public required string Code { get; init; }
        public Uri? RedirectUri { get; init; }
        public string? ClientId { get; init; }                          // provided in form only; optional
        public string? CodeVerifier { get; init; }
        public required TokenClientAuth ClientAuth { get; init; }
    }
}
