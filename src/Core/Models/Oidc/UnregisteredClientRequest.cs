using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines an unregistered client request record.
    /// </summary>
    public sealed record UnregisteredClientRequest(
        Guid RequestId,
        UnregisteredClientRequestStatus Status,
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
