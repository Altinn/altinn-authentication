using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class UpstreamCallbackInput
    {
        public required string? Code { get; init; }
        public required string? State { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }
        public string? Iss { get; init; } // optional

        // Diagnostics
        public required System.Net.IPAddress ClientIp { get; init; }
        public required string? UserAgentHash { get; init; }
        public required Guid CorrelationId { get; init; }
    }
}
