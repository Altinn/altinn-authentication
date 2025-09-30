namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthCodeRow
    {
        public required string Code { get; init; }
        public required string ClientId { get; init; }
        public required Uri RedirectUri { get; init; }
        public required string CodeChallenge { get; init; }
        public required bool Used { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }

        // Subject & session binding (for token claims)
        public required string SubjectId { get; init; }
        public Guid? SubjectPartyUuid { get; init; }
        public int? SubjectPartyId { get; init; }
        public int? SubjectUserId { get; init; }
        public required string SessionId { get; init; }
        public required IReadOnlyCollection<string> Scopes { get; init; }
        public string? Nonce { get; init; }
        public string? Acr { get; init; }
        public DateTimeOffset? AuthTime { get; init; }
        public string CodeChallengeMethod { get; set; }
    }
}
