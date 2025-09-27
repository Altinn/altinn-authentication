using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// High-level outcome of handling an OIDC /authorize request.
    /// The controller turns this into the appropriate HTTP response.
    /// </summary>
    public enum AuthorizeResultKind
    {
        RedirectUpstream,         // 302 to upstream IdP authorize URL
        ErrorRedirectToClient,    // 302 to client's redirect_uri with OIDC error + state
        LocalError,               // Local HTML error (cannot safely redirect)
        RenderInteraction         // Render a view (consent / actor selection)
    }

    /// <summary>
    /// A cookie the controller should set on the response (e.g., login txn cookie).
    /// </summary>
    public sealed class CookieInstruction
    {
        public required string Name { get; init; }
        public required string Value { get; init; }
        public bool HttpOnly { get; init; } = true;
        public bool Secure { get; init; } = true;
        public string? Path { get; init; } = "/";
        public string? Domain { get; init; }
        public DateTimeOffset? Expires { get; init; }
        public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;
    }

    /// <summary>
    /// Service result for /authorize. Use Kind + payload fields relevant for that Kind.
    /// </summary>
    public sealed class AuthorizeResult
    {
        public required AuthorizeResultKind Kind { get; init; }

        // Common metadata (helpful for logs/tracing)
        public Guid? RequestId { get; init; }            // login_transaction.request_id
        public string? CorrelationId { get; init; }      // if you generate/propagate one
        public IReadOnlyList<CookieInstruction> Cookies { get; init; } = Array.Empty<CookieInstruction>();

        // ========== RedirectUpstream payload ==========
        /// <summary>Absolute URL to upstream IdP authorize endpoint.</summary>
        public Uri? UpstreamAuthorizeUrl { get; init; }
        /// <summary>Opaque value your service created to tie upstream callback to our transaction.</summary>
        public string? UpstreamState { get; init; }

        // ========== ErrorRedirectToClient payload ==========
        /// <summary>Absolute redirect_uri for the client (already validated to belong to the client).</summary>
        public Uri? ClientRedirectUri { get; init; }
        /// <summary>OIDC error code (e.g., invalid_request, unauthorized_client, access_denied…)</summary>
        public string? Error { get; init; }
        /// <summary>Human-friendly, non-PII reason. Keep it short.</summary>
        public string? ErrorDescription { get; init; }
        /// <summary>Echo the client's state if it was provided and trustworthy.</summary>
        public string? ClientState { get; init; }

        // ========== LocalError payload ==========
        /// <summary>HTTP status for local error page (e.g., 400/401/403).</summary>
        public int? StatusCode { get; init; }
        /// <summary>Internal/local error code for support and metrics.</summary>
        public string? LocalErrorCode { get; init; }
        /// <summary>Safe message for local error view (no PII, no secrets).</summary>
        public string? LocalErrorMessage { get; init; }

        // ========== RenderInteraction payload ==========
        /// <summary>Name of the MVC view to render (e.g., "Consent", "ActorSelection").</summary>
        public string? ViewName { get; init; }
        /// <summary>Strongly-typed view model for the interaction page.</summary>
        public object? ViewModel { get; init; }

        // -------- Convenience factories --------
        public static AuthorizeResult RedirectUpstream(Uri url, string upstreamState, Guid requestId, IEnumerable<CookieInstruction>? cookies = null)
            => new()
            {
                Kind = AuthorizeResultKind.RedirectUpstream,
                UpstreamAuthorizeUrl = url,
                UpstreamState = upstreamState,
                RequestId = requestId,
                Cookies = (cookies ?? Array.Empty<CookieInstruction>()).ToList()
            };

        public static AuthorizeResult ErrorRedirect(Uri clientRedirectUri, string error, string? description, string? state, Guid? requestId = null, IEnumerable<CookieInstruction>? cookies = null)
            => new()
            {
                Kind = AuthorizeResultKind.ErrorRedirectToClient,
                ClientRedirectUri = clientRedirectUri,
                Error = error,
                ErrorDescription = description,
                ClientState = state,
                RequestId = requestId,
                Cookies = (cookies ?? Array.Empty<CookieInstruction>()).ToList()
            };

        public static AuthorizeResult LocalError(int statusCode, string code, string message, Guid? requestId = null)
            => new()
            {
                Kind = AuthorizeResultKind.LocalError,
                StatusCode = statusCode,
                LocalErrorCode = code,
                LocalErrorMessage = message,
                RequestId = requestId
            };

        public static AuthorizeResult Interaction(string viewName, object viewModel, Guid requestId, IEnumerable<CookieInstruction>? cookies = null)
            => new()
            {
                Kind = AuthorizeResultKind.RenderInteraction,
                ViewName = viewName,
                ViewModel = viewModel,
                RequestId = requestId,
                Cookies = (cookies ?? Array.Empty<CookieInstruction>()).ToList()
            };
    }
}
