using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the parameters for an OIDC authorization request.
    /// </summary>
    public sealed class AuthorizeRequest
    {
        /// <summary>
        /// Gets the response type. Default is "code".
        /// </summary>
        public string ResponseType { get; init; } = "code";

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string? ClientId { get; init; } = default!;

        /// <summary>
        /// Indicates where the authorization server redirects the user-agent after authorization is complete.
        /// </summary>
        public Uri RedirectUri { get; init; } = default!;

        /// <summary>
        /// Specifies the scope of the access request.
        /// </summary>
        public string[] Scopes { get; init; } = [];

        /// <summary>
        /// State parameter to maintain state between the request and callback.
        /// </summary>
        public string? State { get; init; }

        /// <summary>
        /// Nonce parameter to associate a client session with an ID token.
        /// </summary>
        public string? Nonce { get; init; }

        /// <summary>
        /// Code challenge for PKCE.
        /// </summary>
        public string? CodeChallenge { get; init; } = default!;

        /// <summary>
        /// The method used to derive the code challenge. Default is "S256".
        /// </summary>
        public string CodeChallengeMethod { get; init; } = "S256";

        /// <summary>
        /// Authentication context class references.
        /// </summary>
        public string[] AcrValues { get; init; } = [];

        /// <summary>
        /// Prompts to present to the user. Not used in Altinn for now
        /// </summary>
        public string[] Prompts { get; init; } = [];

        /// <summary>
        /// Ui locales for the user interface.
        /// </summary>
        public string[] UiLocales { get; init; } = [];

        /// <summary>
        /// Maximum authentication age. Not used in Altinn for now
        /// </summary>
        public int? MaxAge { get; init; }

        /// <summary>
        /// Gets the URI of the request.
        /// </summary>
        public string? RequestUri { get; init; }

        /// <summary>
        /// Request object in JWT format. Not supported in Altinn for now
        /// </summary>
        public string? RequestObject { get; init; }

        /// <summary>
        /// Response mode. Not used in Altinn for now
        /// </summary>
        public string? ResponseMode { get; init; }

        /// <summary>
        /// Login hint to pre-fill the username/email field of the sign-in page. Not used in Altinn for now
        /// </summary>
        public string? LoginHint { get; init; }

        /// <summary>
        /// ID Token hint. Not used in Altinn for now
        /// </summary>
        public string? IdTokenHint { get; init; }

        /// <summary>
        /// Claims parameter in JSON format. 
        /// </summary>
        public string? ClaimsJson { get; init; }

        /// <summary>
        /// Claims locales.
        /// </summary>
        public string? ClaimsLocales { get; init; }

        /// <summary>
        /// Authorization details in JSON format.
        /// </summary>
        public string? AuthorizationDetailsJson { get; init; }

        /// <summary>
        /// Resource parameter.
        /// </summary>
        public string? Resource { get; init; }

        /// <summary>
        /// Ip address of the client making the request.
        /// </summary>
        public IPAddress? ClientIp { get; init; }

        /// <summary>
        /// User agent hash of the client making the request.
        /// </summary>
        public string? UserAgentHash { get; init; }

        /// <summary>
        /// Correlation ID for tracking the request.
        /// </summary>
        public Guid? CorrelationId { get; init; }
    }
}
