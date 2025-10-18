using System;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed record RefreshTokenRow: OidcBindingContextBase
    {
        public Guid TokenId { get; init; }
        public Guid FamilyId { get; init; }

        // Lifecycle / status
        public string Status { get; init; } = "active"; // 'active' | 'used' | 'rotated' | 'revoked'
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset AbsoluteExpiresAt { get; init; }
        public DateTimeOffset? RevokedAt { get; init; }
        public string? RevokedReason { get; init; }

        // Rotation linkage
        public Guid? RotatedToTokenId { get; init; }

        // Cryptographic material
        public byte[] LookupKey { get; init; } = Array.Empty<byte>(); // HMAC(pepper, token)
        public byte[] Hash { get; init; } = Array.Empty<byte>();      // PBKDF2-SHA256
        public byte[] Salt { get; init; } = Array.Empty<byte>();
        public int Iterations { get; init; }

        public string OpSid { get; init; } = string.Empty; // FK → oidc_session.sid

        // Diagnostics / metadata
        public string? UserAgentHash { get; init; }
        public string? IpHash { get; init; }
    }
}
