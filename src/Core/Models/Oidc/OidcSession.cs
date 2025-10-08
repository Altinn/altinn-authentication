namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the OIDC session established after a successful authentication.
    /// </summary>
    public sealed class OidcSession
    {
        /// <summary>
        /// The session ID (SID) claim as per OIDC spec.
        /// </summary>
        public required string Sid { get; init; }

        /// <summary>
        /// Hash of session Handle 
        /// </summary>
        public required byte[] SessionHandle { get; init; }  // internal reference

        /// <summary>
        /// The upstream issuer (iss claim) of the authenticated user. Values from ID-porten, UIDP, test providers, etc.
        /// </summary>
        public required string UpstreamIssuer { get; init; }

        /// <summary>
        /// Gets the upstream subject identifier. This is the 'sub' claim from the upstream identity provider.
        /// </summary>
        public required string UpstreamSub { get; init; }
        
        /// <summary>
        /// Gets the unique identifier for the subject, such as a PID, email, or other policy-defined value.
        /// </summary>
        public required string SubjectId { get; init; }

        /// <summary>
        /// Unique identifier for the subject party (organization) if applicable. Owned by Altinn Register
        /// </summary>
        public Guid? SubjectPartyUuid { get; init; }

        /// <summary>
        /// Unique identifier for the subject user if applicable. Owned by Altinn Register
        /// </summary>
        public int? SubjectPartyId { get; init; }

        /// <summary>
        /// Subject user id if applicable. Owned by Altinn Register. Legacy from Altinn 2
        /// </summary>
        public int? SubjectUserId { get; init; }

        /// <summary>
        /// Key of the configured identity provider used for authentication.
        /// </summary>
        public required string Provider { get; init; }

        /// <summary>
        /// Authentication context class reference as per OIDC spec (acr claim).
        /// </summary>
        public string? Acr { get; init; }

        /// <summary>
        /// The time when the user was authenticated (auth_time claim) as per OIDC spec.
        /// </summary>
        public DateTimeOffset? AuthTime { get; init; }

        /// <summary>
        /// Amr claim as per OIDC spec. Array of strings describing the authentication methods used.
        /// </summary>
        public string[]? Amr { get; init; }

        /// <summary>
        /// Scopes granted to the client for this session.
        /// </summary>
        public string[] Scopes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// The time when the session was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// The time when the session was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; }
        public DateTimeOffset? LastSeenAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }

        // Logout binding
        public string? UpstreamSessionSid { get; init; }
    }
}
