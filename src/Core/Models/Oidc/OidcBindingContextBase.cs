using System;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Common OIDC binding/context carried by grant artifacts (auth code, refresh token, etc.).
    /// Does not include lifecycle or cryptographic fields.
    /// </summary>
    public abstract record OidcBindingContextBase
    {
        // Binding
        public required string ClientId { get; init; }
        public required string SubjectId { get; init; }

        public string? ExternalId { get; init; }

        /// <summary>
        /// Server-side OP session id this artifact is bound to.
        /// (Harmonized name used across models.)
        /// </summary>
        public required string SessionId { get; init; }

        // Subject context
        public Guid? SubjectPartyUuid { get; init; }
        public int? SubjectPartyId { get; init; }
        public int? SubjectUserId { get; init; }

        public string? SubjectUserName { get; init; }

        // Token issuance context
        public string[] Scopes { get; init; } = Array.Empty<string>();
        public string? Acr { get; init; }
        public string[]? Amr { get; init; }

        public Dictionary<string, List<string>>? ProviderClaims { get; set; }

        public DateTimeOffset? AuthTime { get; init; }

        // Subject & session binding (for token claims)
        public string? Nonce { get; init; }
    }
}
