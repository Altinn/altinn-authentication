namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Input model for inserting a new OIDC client. All values must be normalized/validated by the caller.
    /// Clients in Altinn are always confidential clients.
    /// Example clients is Arbeidsflate. In the future we might support that every app in Altinn Apps can be a client.
    /// This would allow that apps can run outside Altinn Apps clusters.
    /// </summary>
    public sealed class OidcClientCreate
    {
        /// <summary>
        /// The unique client Id for clients registered with the authorization server.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The human-readable name of the client to be presented to the end-user during authorization.
        /// </summary>
        public required string ClientName { get; init; }

        /// <summary>
        /// The type of client. Defaults to "confidential" if not specified. Altinn only supports confidential clients.
        /// </summary>
        public ClientType ClientType { get; init; } = ClientType.Confidential;

        /// <summary>
        /// Gets the authentication method used for the token endpoint. Defaults to client_secret_basic. 
        /// </summary>
        public TokenEndpointAuthMethod TokenEndpointAuthMethod { get; init; } = TokenEndpointAuthMethod.ClientSecretBasic;
       
        /// <summary>Absolute redirect URIs.</summary>
        public required IReadOnlyCollection<Uri> RedirectUris { get; init; }

        /// <summary>Allowed scopes, lowercased.</summary>
        public required IReadOnlyCollection<string> AllowedScopes { get; init; }

        /// <summary>Hash of client secret (never plaintext). Null if using private_key_jwt only.</summary>
        public string? ClientSecretHash { get; init; }

        /// <summary>
        /// Gets the expiration date and time of the client secret.
        /// </summary>
        public DateTimeOffset? ClientSecretExpiresAt { get; init; }

        /// <summary>
        /// Gets the date and time when the secret was last rotated, if available.
        /// </summary>
        public DateTimeOffset? SecretRotationAt { get; init; }

        /// <summary>
        /// The URI of the JSON Web Key Set document that contains the client's public keys.
        /// </summary>
        public Uri? JwksUri { get; init; }

        /// <summary>
        /// The JSON Web Key Set document that contains the client's public keys.
        /// </summary>
        public string? JwksJson { get; init; } // JSON (string) stored into jsonb

        public string? FrontchannelLogoutUri { get; init; }

        public string? BackchannelLogoutUri { get; init; }

    }
}
