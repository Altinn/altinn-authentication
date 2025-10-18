using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed record ClientlessRequest(
        Guid RequestId,
        ClientlessRequestStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? CompletedAt,
        string Issuer,
        string GotoUrl,
        Guid? UpstreamRequestId,
        IPAddress? CreatedByIp,
        string? UserAgentHash,
        Guid? CorrelationId,
        string? HandledByCallback
    );
}
