namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class OidcSessionCreate
    {
        public required string Sid { get; init; }

        public required byte[] SessionHandleHash { get; init; }  // internal reference

        // Upstream identity
        public required string UpstreamIssuer { get; init; }
        public required string UpstreamSub { get; init; }
        public string? SubjectId { get; init; }        // urn:altinn:party:uuid:23423.-32

        public string? ExternalId { get; init; }           // PID/email/etc

        // Altinn mapping
        public Guid? SubjectPartyUuid { get; init; }
        public int? SubjectPartyId { get; init; }
        public int? SubjectUserId { get; init; }

        public string? SubjectUserName { get; init; }

        // Auth props
        public required string Provider { get; init; }
        public string? Acr { get; init; }
        public DateTimeOffset? AuthTime { get; init; }
        public string[]? Amr { get; init; }

        public required IReadOnlyCollection<string> Scopes { get; init; }     // lowercased/distinct

        // Lifecycle / diagnostics
        public DateTimeOffset? ExpiresAt { get; init; }
        public DateTimeOffset? Now { get; init; }
        public string? UpstreamSessionSid { get; init; }
        public System.Net.IPAddress? CreatedByIp { get; init; }
        public string? UserAgentHash { get; init; }
    }
}
