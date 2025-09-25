namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// How the client authenticates to the OP's /token endpoint.
    /// </summary>
    public enum TokenEndpointAuthMethod
    {
        ClientSecretBasic,
        ClientSecretPost,
        PrivateKeyJwt,
        None
    }

    /// <summary>
    /// High-level client type (affects security policy).
    /// </summary>
    public enum ClientType
    {
        Confidential,
        Public,
        Mtls,              // reserved for future
        PrivateKeyJwtOnly  // convenience marker for policy
    }

    /// <summary>
    /// OIDC subject identifier strategy for ID tokens issued to this client.
    /// </summary>
    public enum SubjectType
    {
        Public,
        Pairwise
    }

    /// <summary>
    /// Repository return model for a registered OIDC client (RP).
    /// Immutable, safe to cache in-memory for short durations.
    /// </summary>
    public sealed class OidcClient
    {
        /// <summary>Unique client identifier (as used on authorize/token).</summary>
        public string ClientId { get; }

        /// <summary>Human-readable client name (logging/admin only).</summary>
        public string ClientName { get; }

        /// <summary>If false, this client is blocked.</summary>
        public bool Enabled { get; }

        /// <summary>Confidential/Public/etc. Impacts allowed auth methods and PKCE rules.</summary>
        public ClientType ClientType { get; }

        /// <summary>How the client is allowed to authenticate to /token.</summary>
        public TokenEndpointAuthMethod TokenEndpointAuthMethod { get; }

        /// <summary>Exact list of allowed redirect URIs for /authorize.</summary>
        public IReadOnlyCollection<Uri> RedirectUris { get; }

        /// <summary>Optional post-logout redirect URIs.</summary>
        public IReadOnlyCollection<Uri> PostLogoutRedirectUris { get; }

        /// <summary>Back-channel logout endpoint (optional).</summary>
        public Uri? BackchannelLogoutUri { get; }

        /// <summary>Front-channel logout endpoint (optional, used for iframe fallback).</summary>
        public Uri? FrontchannelLogoutUri { get; }

        /// <summary>Allowed scopes for this client (lowercase, space tokens).</summary>
        public IReadOnlyCollection<string> AllowedScopes { get; }

        /// <summary>Require PKCE for this client (recommended true for all).</summary>
        public bool RequirePkce { get; }

        /// <summary>Allowed PKCE methods, normally just "S256".</summary>
        public IReadOnlyCollection<string> AllowedCodeChallengeMethods { get; }

        /// <summary>Require nonce on authorize (recommended true).</summary>
        public bool RequireNonce { get; }

        /// <summary>Optional: consent required before issuing code (if you implement consent).</summary>
        public bool RequireConsent { get; }

        /// <summary>Optional: require actor selection (stort aktørvalg) before issuing code.</summary>
        public bool RequireActorSelection { get; }

        /// <summary>Subject type (public/pairwise) for ID tokens issued to this client.</summary>
        public SubjectType SubjectType { get; }

        /// <summary>Sector identifier URI for pairwise subject calculation (optional).</summary>
        public Uri? SectorIdentifierUri { get; }

        /// <summary>Salt used for pairwise sub derivation (if you don’t use sector identifier file).</summary>
        public string? PairwiseSalt { get; }

        /// <summary>Hash of the client secret (Argon2/BCrypt). Null if none.</summary>
        public string? ClientSecretHash { get; }

        /// <summary>When the current secret expires (optional).</summary>
        public DateTimeOffset? ClientSecretExpiresAt { get; }

        /// <summary>When the secret was last rotated (optional).</summary>
        public DateTimeOffset? SecretRotationAt { get; }

        /// <summary>JWKS URI for validating client assertions (private_key_jwt).</summary>
        public Uri? JwksUri { get; }

        /// <summary>Inline JWKS (optional) if you store keys directly.</summary>
        public string? JwksJson { get; }

        /// <summary>Allow routing to Test-IDP (feature/policy).</summary>
        public bool AllowTestIdp { get; }

        /// <summary>Require PAR when using Test-IDP (extra safety).</summary>
        public bool RequireParForTestIdp { get; }

        /// <summary>Creation timestamp.</summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>Last update timestamp.</summary>
        public DateTimeOffset? UpdatedAt { get; }

        public OidcClient(
            string clientId,
            string clientName,
            bool enabled,
            ClientType clientType,
            TokenEndpointAuthMethod tokenEndpointAuthMethod,
            IEnumerable<Uri> redirectUris,
            IEnumerable<string> allowedScopes,
            bool requirePkce,
            IEnumerable<string>? allowedCodeChallengeMethods = null,
            bool requireNonce = true,
            bool requireConsent = false,
            bool requireActorSelection = false,
            SubjectType subjectType = SubjectType.Public,
            Uri? sectorIdentifierUri = null,
            string? pairwiseSalt = null,
            string? clientSecretHash = null,
            DateTimeOffset? clientSecretExpiresAt = null,
            DateTimeOffset? secretRotationAt = null,
            Uri? jwksUri = null,
            string? jwksJson = null,
            IEnumerable<Uri>? postLogoutRedirectUris = null,
            Uri? backchannelLogoutUri = null,
            Uri? frontchannelLogoutUri = null,
            bool allowTestIdp = false,
            bool requireParForTestIdp = true,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? updatedAt = null)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
            Enabled = enabled;
            ClientType = clientType;
            TokenEndpointAuthMethod = tokenEndpointAuthMethod;

            RedirectUris = (redirectUris ?? throw new ArgumentNullException(nameof(redirectUris)))
                .ToArray();
            if (RedirectUris.Count == 0) throw new ArgumentException("At least one redirect URI is required.", nameof(redirectUris));
            if (RedirectUris.Any(u => !u.IsAbsoluteUri)) throw new ArgumentException("Redirect URIs must be absolute.", nameof(redirectUris));

            PostLogoutRedirectUris = (postLogoutRedirectUris ?? Array.Empty<Uri>()).ToArray();

            AllowedScopes = (allowedScopes ?? throw new ArgumentNullException(nameof(allowedScopes)))
                .Select(s => s?.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToArray();

            RequirePkce = requirePkce;
            AllowedCodeChallengeMethods = (allowedCodeChallengeMethods ?? new[] { "S256" })
                .Select(m => m?.ToUpperInvariant())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToArray();

            RequireNonce = requireNonce;
            RequireConsent = requireConsent;
            RequireActorSelection = requireActorSelection;

            SubjectType = subjectType;
            SectorIdentifierUri = sectorIdentifierUri;
            PairwiseSalt = pairwiseSalt;

            ClientSecretHash = clientSecretHash;
            ClientSecretExpiresAt = clientSecretExpiresAt;
            SecretRotationAt = secretRotationAt;

            JwksUri = jwksUri;
            JwksJson = jwksJson;

            BackchannelLogoutUri = backchannelLogoutUri;
            FrontchannelLogoutUri = frontchannelLogoutUri;

            AllowTestIdp = allowTestIdp;
            RequireParForTestIdp = requireParForTestIdp;

            CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
            UpdatedAt = updatedAt;
        }

        // ---------------- Helper checks used by Service layer ----------------

        /// <summary>Returns true when the given redirect URI is exactly registered on this client.</summary>
        public bool IsRedirectUriAllowed(Uri redirectUri)
            => redirectUri is not null && RedirectUris.Contains(redirectUri);

        /// <summary>Returns true when all requested scopes are allowed (case-insensitive).</summary>
        public bool AreScopesAllowed(IEnumerable<string> requestedScopes)
        {
            var req = (requestedScopes ?? Array.Empty<string>())
                .Select(s => s?.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            return req.All(AllowedScopes.Contains);
        }

        /// <summary>Returns true if this client is configured to use a secret (for token endpoint).</summary>
        public bool SupportsClientSecret()
            => TokenEndpointAuthMethod is TokenEndpointAuthMethod.ClientSecretBasic or TokenEndpointAuthMethod.ClientSecretPost;

        /// <summary>Returns true if this client is configured for private_key_jwt.</summary>
        public bool SupportsPrivateKeyJwt()
            => TokenEndpointAuthMethod == TokenEndpointAuthMethod.PrivateKeyJwt;

        /// <summary>Returns true if PKCE method is allowed (normally only S256).</summary>
        public bool IsPkceMethodAllowed(string? method)
            => !RequirePkce || AllowedCodeChallengeMethods.Contains((method ?? "S256").ToUpperInvariant());

        /// <summary>Whether the current secret (if any) is expired.</summary>
        public bool IsSecretExpired(DateTimeOffset nowUtc)
            => ClientSecretExpiresAt is not null && ClientSecretExpiresAt <= nowUtc;
    }
}
