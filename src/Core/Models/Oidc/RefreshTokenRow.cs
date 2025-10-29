using System;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed record RefreshTokenRow: OidcBindingContextBase
    {
        /// <summary>
        /// The unique identifier for the refresh token.
        /// </summary>
        public Guid TokenId { get; init; }

        /// <summary>
        /// The family identifier for the refresh token.
        /// </summary>
        public Guid FamilyId { get; init; }

        /// <summary>
        /// The status of the refresh token.
        /// </summary>
        public string Status { get; init; } = "active"; // 'active' | 'used' | 'rotated' | 'revoked'

        /// <summary>
        /// When the refresh token was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// When the refresh token expires.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// When the refresh token absolutely expires (including grace period).
        /// </summary>
        public DateTimeOffset AbsoluteExpiresAt { get; init; }

        /// <summary>
        /// When the refresh token was revoked.
        /// </summary>
        public DateTimeOffset? RevokedAt { get; init; }

        /// <summary>
        /// The revocation reason, if revoked.
        /// </summary>
        public string? RevokedReason { get; init; }

        /// <summary>
        /// Refers to the new token id if this token has been rotated.
        /// </summary>
        public Guid? RotatedToTokenId { get; init; }

        /// <summary>
        /// The lookup key for the refresh token.
        /// </summary>
        [JsonIgnore] 
        public byte[] LookupKey { get; init; } = Array.Empty<byte>(); // HMAC(pepper, token)

        /// <summary>
        /// The hash of the refresh token value.
        /// </summary>
        [JsonIgnore] 
        public byte[] Hash { get; init; } = Array.Empty<byte>();      // PBKDF2-SHA256

        /// <summary>
        /// The salt used in the PBKDF2 hashing of the refresh token.
        /// </summary>
        [JsonIgnore] 
        public byte[] Salt { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Number of iterations used in the PBKDF2 hashing of the refresh token.
        /// </summary>
        public required int Iterations { get; init; } // e.g., 300_000

        /// <summary>
        /// The hash of the user agent from which the refresh token was issued.
        /// </summary>
        public string? UserAgentHash { get; init; }

        /// <summary>
        /// The hash of the IP address from which the refresh token was issued.
        /// </summary>
        public string? IpHash { get; init; }
    }
}
