using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed record ClientlessRequestCreate(
        Guid RequestId,
        DateTimeOffset ExpiresAt,
        string Issuer,
        string GotoUrl,
        IPAddress? CreatedByIp,
        string? UserAgentHash,
        Guid? CorrelationId
    );
}
