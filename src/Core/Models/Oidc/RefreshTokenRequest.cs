namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Represents a request to the token endpoint using the refresh_token grant type.
    /// </summary>
    public sealed class RefreshTokenRequest
    {
        /// <summary>
        /// The type of grant being requested. For this class, it must be "refresh_token".
        /// </summary>
        public required string GrantType { get; init; }

        /// <summary>
        /// The refresh token issued to the client.
        /// </summary>
        public required string RefreshToken { get; init; }

        /// <summary>
        /// Optional scope to down-scope the issued tokens.
        /// TODO: Do we need to support this?
        /// </summary>
        public string? Scope { get; init; }

        /// <summary>
        /// The client identifier. May be omitted if client authentication is provided via other means (e.g., Basic Auth).
        /// </summary>
        public string? ClientId { get; init; }

        /// <summary>
        /// Gets the client authentication configuration used to authenticate requests to the token endpoint.
        /// </summary>
        public TokenClientAuth ClientAuth { get; init; } = default!;
    }
}
