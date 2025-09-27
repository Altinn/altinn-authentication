using System;
using System.Collections.Generic;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Input model for inserting a new OIDC client. All values must be normalized/validated by the caller.
    /// Store only a HASH for client secret here (never plaintext).
    /// </summary>
    public sealed class OidcClientCreate
    {
        public required string ClientId { get; init; }
        public required string ClientName { get; init; }
        public ClientType ClientType { get; init; } = ClientType.Confidential;
        public TokenEndpointAuthMethod TokenEndpointAuthMethod { get; init; } = TokenEndpointAuthMethod.ClientSecretBasic;

        /// <summary>Absolute redirect URIs.</summary>
        public required IReadOnlyCollection<Uri> RedirectUris { get; init; }

        /// <summary>Allowed scopes, lowercased.</summary>
        public required IReadOnlyCollection<string> AllowedScopes { get; init; }

        /// <summary>Hash of client secret (never plaintext). Null if using private_key_jwt only.</summary>
        public string? ClientSecretHash { get; init; }
        public DateTimeOffset? ClientSecretExpiresAt { get; init; }
        public DateTimeOffset? SecretRotationAt { get; init; }

        public Uri? JwksUri { get; init; }
        public string? JwksJson { get; init; } // JSON (string) stored into jsonb

        // Optional future fields (post_logout, backchannel, etc.) can be added later
    }
}
