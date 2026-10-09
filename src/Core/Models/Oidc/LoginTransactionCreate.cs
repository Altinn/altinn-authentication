using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class LoginTransactionCreate
    {
        // lifecycle
        public DateTimeOffset ExpiresAt { get; init; }             // NOW + 10 minutes (or from policy)
        public Guid? CorrelationId { get; init; }

        // client binding
        public required string ClientId { get; init; }
        public required Uri RedirectUri { get; init; }

        // core OIDC params
        public required IReadOnlyCollection<string> Scopes { get; init; }     // lowercased/distinct
        public required string State { get; init; }
        public required string Nonce { get; init; }
        public IReadOnlyCollection<string>? AcrValues { get; init; }          // optional
        public IReadOnlyCollection<string>? Prompts { get; init; }            // optional
        public IReadOnlyCollection<string>? UiLocales { get; init; }          // optional
        public int? MaxAge { get; init; }

        // PKCE
        public required string CodeChallenge { get; init; }
        public string CodeChallengeMethod { get; init; } = "S256";

        // Advanced / optional
        public string? RequestUri { get; init; }                // PAR
        public string? RequestObjectJwt { get; init; }          // JAR
        public string? AuthorizationDetailsJson { get; init; }  // JSON string -> jsonb

        public IPAddress? CreatedByIp { get; init; }
        public string? UserAgentHash { get; init; }
    }
}
