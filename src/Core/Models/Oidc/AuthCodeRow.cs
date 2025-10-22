namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines an authorization code issued to a client after successful user authentication.
    /// </summary>
    public sealed record AuthCodeRow : OidcBindingContextBase
    {
        /// <summary>
        /// The authorization code value.
        /// </summary>
        public required string Code { get; init; }

        /// <summary>
        /// The client identifier for which the code was issued.
        /// </summary>
        public required Uri RedirectUri { get; init; }

        /// <summary>
        /// The PKCE code challenge associated with the authorization code.
        /// </summary>
        public required string CodeChallenge { get; init; }

        /// <summary>
        /// Defines whether the authorization code has been used.
        /// </summary>
        public required bool Used { get; init; }

        /// <summary>
        /// Gets the expiration date and time for the current object.
        /// </summary>
        public required DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// Defines the PKCE code challenge method. Defaults to "S256".
        /// </summary>
        public string CodeChallengeMethod { get; init; } = "S256";
    }
}
