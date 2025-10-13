using Microsoft.Identity.Client;
using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class ClientlessRequestCreate
    {
        public Guid RequestId { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        public string Issuer {  get; init; }

        public string? GotoUrl {  get; init; }

        public IPAddress? CreatedByIp { get; init; }

        public string? UserAgentHash { get; set; } 

        public Guid? CorrelationId { get; init; }
    }
}
