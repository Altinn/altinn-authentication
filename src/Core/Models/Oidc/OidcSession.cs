using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class OidcSession
    {
        public required string Sid { get; init; }

        // Upstream identity
        public required string UpstreamIssuer { get; init; }
        public required string UpstreamSub { get; init; }
        public required string SubjectId { get; init; }            // PID/email/etc (policy-defined)

        // Altinn mapping
        public Guid? SubjectPartyUuid { get; init; }
        public int? SubjectPartyId { get; init; }
        public int? SubjectUserId { get; init; }

        // Auth props
        public required string Provider { get; init; }
        public string? Acr { get; init; }   
        public DateTimeOffset? AuthTime { get; init; }
        public string[]? Amr { get; init; }

        public string[] Scopes { get; init; } = Array.Empty<string>();

        // Lifecycle
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public DateTimeOffset? LastSeenAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }

        // Logout binding
        public string? UpstreamSessionSid { get; init; }
    }
}
