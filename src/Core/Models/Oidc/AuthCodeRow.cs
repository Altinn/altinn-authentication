namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed record AuthCodeRow : OidcBindingContextBase
    {
        public required string Code { get; init; }

        public required Uri RedirectUri { get; init; }
        public required string CodeChallenge { get; init; }
        public required bool Used { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public string CodeChallengeMethod { get; set; }
    }
}
