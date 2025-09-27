using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthorizeRequest
    {
        public string ResponseType { get; init; } = "code";
        public string ClientId { get; init; } = default!;
        public Uri RedirectUri { get; init; } = default!;
        public string[] Scopes { get; init; } = Array.Empty<string>();
        public string? State { get; init; }
        public string? Nonce { get; init; }
        public string CodeChallenge { get; init; } = default!;
        public string CodeChallengeMethod { get; init; } = "S256";
        public string[] AcrValues { get; init; } = Array.Empty<string>();
        public string[] Prompts { get; init; } = Array.Empty<string>();
        public string[] UiLocales { get; init; } = Array.Empty<string>();
        public int? MaxAge { get; init; }
        public string? RequestUri { get; init; }
        public string? RequestObject { get; init; }
        public string? ResponseMode { get; init; }
        public string? LoginHint { get; init; }
        public string? IdTokenHint { get; init; }
        public string? ClaimsJson { get; init; }
        public string? ClaimsLocales { get; init; }
        public string? AuthorizationDetailsJson { get; init; }
        public string? Resource { get; init; }

        // New: captured server-side (not from query)
        public IPAddress? ClientIp { get; init; }
        public string? UserAgentHash { get; init; }
        public Guid? CorrelationId { get; init; }
    }
}
