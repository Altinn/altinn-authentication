namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the data required to create an OIDC authorization code.
    /// </summary>
    public sealed class AuthorizationCodeCreate
    {
        /// <summary>
        /// The authorization code value. Will be used by the downstream client to redeem tokens. base64url, ≥128 bits
        /// </summary>
        public required string Code { get; init; }

        /// <summary>
        /// The client identifier for which the authorization code is issued.
        /// </summary>        
        public required string ClientId { get; init; }

        /// <summary>
        /// The unique partyurn for the specific subject. Owned by Altinn Register example: urn:altinn:party:uuid:{partuuid}
        /// </summary>
        public required string SubjectId { get; init; }       

        /// <summary>
        /// The global unique identifier for the subject, such as a PID, email, or other policy-defined value that externally identifies the user.
        /// </summary>
        public string? ExternalId { get; init; }   // upstream external id (pid/email)

        /// <summary>
        /// The unique identifier for the subject party (organization) if applicable. Owned by Altinn Register
        /// </summary>
        public Guid? SubjectPartyUuid { get; init; }

        /// <summary>
        /// The unique identifier for the subject party (organization/person++) if applicable. Owned by Altinn Register
        /// </summary>
        public int? SubjectPartyId { get; init; }

        /// <summary>
        /// The subject user id if applicable. Owned by Altinn Register. Legacy from Altinn 2
        /// </summary>
        public int? SubjectUserId { get; init; }

        /// <summary>
        /// Gets the user name of the subject associated with the current context.
        /// </summary>
        public string? SubjectUserName { get; init; }

        /// <summary>
        /// The unique session identifier associated with the authorization code.
        /// </summary>
        public required string SessionId { get; init; }            // oidc_session.sid

        /// <summary>
        /// The redirect URI associated with the authorization code.
        /// </summary>
        public required Uri RedirectUri { get; init; }

        /// <summary>
        /// The scopes associated with the authorization code.
        /// </summary>
        public required IReadOnlyCollection<string> Scopes { get; init; }

        /// <summary>
        /// Gets the unique, one-time-use value associated with the current operation.
        /// </summary>
        public string? Nonce { get; init; }

        /// <summary>
        /// Gets the Authentication Context Class Reference (ACR) value associated with the authentication.
        /// </summary>
        public string? Acr { get; init; }

        /// <summary>
        /// List of custom claims associated with the session. Defined per upstream identity provider.
        /// </summary>
        public Dictionary<string, List<string>>? ProviderClaims { get; set; }

        /// <summary>
        /// The Amr claim as per OIDC spec. Array of strings describing the authentication methods used.
        /// </summary>
        public IReadOnlyCollection<string>? Amr { get; init; }

        /// <summary>
        /// Authentication time (auth_time claim) as per OIDC spec.
        /// </summary>
        public DateTimeOffset? AuthTime { get; init; }

        /// <summary>
        /// Code challenge for PKCE support.
        /// </summary>
        public required string CodeChallenge { get; init; }        // from downstream request

        /// <summary>
        /// Code challenge method for PKCE support.
        /// </summary>
        public string CodeChallengeMethod { get; init; } = "S256";
        
        /// <summary>
        /// Gets the date and time at which the token was issued.
        /// </summary>
        public required DateTimeOffset IssuedAt { get; init; }

        /// <summary>
        /// Gets the date and time at which the item expires.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// The IP address from which the authorization code was reqestd.
        /// </summary>
        public System.Net.IPAddress? CreatedByIp { get; init; }

        /// <summary>
        /// The correlation identifier for tracing the request through various components.
        /// </summary>
        public Guid? CorrelationId { get; init; }
    }
}
