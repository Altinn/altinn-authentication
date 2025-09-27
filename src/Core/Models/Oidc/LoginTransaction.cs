using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Row shape for the persisted transaction (what we RETURN from DB).
    /// </summary>
    public sealed class LoginTransaction
    {
        public required Guid RequestId { get; init; }
        public required string Status { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }

        public required string ClientId { get; init; }
        public required Uri RedirectUri { get; init; }

        public required IReadOnlyCollection<string> Scopes { get; init; }
        public required string State { get; init; }
        public string? Nonce { get; init; }
        public IReadOnlyCollection<string>? AcrValues { get; init; }
        public IReadOnlyCollection<string>? Prompts { get; init; }
        public IReadOnlyCollection<string>? UiLocales { get; init; }
        public int? MaxAge { get; init; }

        public required string CodeChallenge { get; init; }
        public required string CodeChallengeMethod { get; init; }

        public string? RequestUri { get; init; }
        public string? RequestObjectJwt { get; init; }
        public string? AuthorizationDetailsJson { get; init; }

        public string? OriginalRequestUrl { get; init; }
        public IPAddress? CreatedByIp { get; init; }
        public string? UserAgentHash { get; init; }
        public Guid? CorrelationId { get; init; }

        public Guid? UpstreamRequestId { get; init; }
    }
}
