using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamLoginTransactionCreate
    {
        // FK to downstream request
        public Guid RequestId { get; init; }

        /// FK to client less request
        public Guid UnregisteredClientRequestId { get; init; }

        // lifecycle
        public required DateTimeOffset ExpiresAt { get; init; }   // e.g., now + 10 min

        // which upstream we target
        public required string Provider { get; init; }            // e.g., "idporten"
        public required string UpstreamClientId { get; init; }
        public required Uri AuthorizationEndpoint { get; init; }
        public required Uri TokenEndpoint { get; init; }
        public Uri? JwksUri { get; init; }

        // our upstream callback
        public required Uri UpstreamRedirectUri { get; init; }

        // effective upstream request params
        public required string State { get; init; }               // upstream_state
        public required string Nonce { get; init; }               // upstream_nonce
        public required string[] Scopes { get; init; }            // e.g. ["openid"]
        public string[]? AcrValues { get; init; }
        public string[]? Prompts { get; init; }
        public string[]? UiLocales { get; init; }
        public int? MaxAge { get; init; }

        // PKCE
        public required string CodeVerifier { get; init; }        // 43–128
        public required string CodeChallenge { get; init; }
        public string CodeChallengeMethod { get; init; } = "S256";

        // diagnostics
        public Guid? CorrelationId { get; init; }
        public IPAddress? CreatedByIp { get; init; }
        public string? UserAgentHash { get; init; }
    }
}
