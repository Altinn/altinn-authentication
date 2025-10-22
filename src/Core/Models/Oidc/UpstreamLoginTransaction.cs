using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamLoginTransaction
    {
        public required Guid UpstreamRequestId { get; init; }
        
        public Guid? RequestId { get; init; }

        public Guid? UnregisteredClientRequestId { get; set; }


        public required string Status { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }

        public required string Provider { get; init; }
        public required string UpstreamClientId { get; init; }
        public required Uri AuthorizationEndpoint { get; init; }
        public required Uri TokenEndpoint { get; init; }
        public Uri? JwksUri { get; init; }

        public required Uri UpstreamRedirectUri { get; init; }

        public required string State { get; init; }
        public required string Nonce { get; init; }
        public required string[] Scopes { get; init; }
        public string[]? AcrValues { get; init; }
        public string[]? Prompts { get; init; }
        public string[]? UiLocales { get; init; }
        public int? MaxAge { get; init; }

        public required string CodeVerifier { get; init; }
        public required string CodeChallenge { get; init; }
        public required string CodeChallengeMethod { get; init; }

        // callback
        public string? AuthCode { get; init; }
        public DateTimeOffset? AuthCodeReceivedAt { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }

        // token result
        public DateTimeOffset? TokenExchangedAt { get; init; }
        public string? UpstreamIssuer { get; init; }
        public string? UpstreamSub { get; init; }
        public string? UpstreamAcr { get; init; }
        public DateTimeOffset? UpstreamAuthTime { get; init; }
        public string? UpstreamIdTokenJti { get; init; }
        public string? UpstreamSessionSid { get; init; }

        // diagnostics
        public Guid? CorrelationId { get; init; }
        public IPAddress? CreatedByIp { get; init; }
        public string? UserAgentHash { get; init; }
    }
}
