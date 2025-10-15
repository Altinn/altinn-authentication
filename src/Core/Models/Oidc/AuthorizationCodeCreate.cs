namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    // Core/Models/Oidc/AuthorizationCode.cs
    public sealed class AuthorizationCodeCreate
    {
        public required string Code { get; init; }                 // base64url, ≥128 bits
        public required string ClientId { get; init; }
        public required string SubjectId { get; init; }       //urn:altinn:party:uuid:{partuudi}

        public string? ExternalId { get; init; }   // upstream external id (pid/email)
        public Guid? SubjectPartyUuid { get; init; }
        public int? SubjectPartyId { get; init; }
        public int? SubjectUserId { get; init; }
        public required string SessionId { get; init; }            // oidc_session.sid
        public required Uri RedirectUri { get; init; }
        public required IReadOnlyCollection<string> Scopes { get; init; }
        public string? Nonce { get; init; }
        public string? Acr { get; init; }
        public IReadOnlyCollection<string>? Amr { get; init; }
        public DateTimeOffset? AuthTime { get; init; }
        public required string CodeChallenge { get; init; }        // from downstream request
        public string CodeChallengeMethod { get; init; } = "S256";
        public required DateTimeOffset IssuedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public System.Net.IPAddress? CreatedByIp { get; init; }
        public Guid? CorrelationId { get; init; }
    }
}
