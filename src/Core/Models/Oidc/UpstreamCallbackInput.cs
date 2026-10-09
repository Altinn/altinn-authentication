namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the input received at the upstream OIDC callback endpoint.
    /// </summary>
    public sealed class UpstreamCallbackInput
    {
        /// <summary>
        /// The authorization code returned by the upstream identity provider.
        /// </summary>
        public required string? Code { get; init; }

        /// <summary>
        /// The state parameter returned by the upstream identity provider.
        /// </summary>
        public required string? State { get; init; }

        /// <summary>
        /// Error code returned by the upstream identity provider.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Error description returned by the upstream identity provider.
        /// </summary>
        public string? ErrorDescription { get; init; }

        /// <summary>
        /// Issuer identifier of the upstream identity provider.
        /// </summary>
        public string? Iss { get; init; } // optional

        /// <summary>
        /// IP address of the client making the request.
        /// </summary>
        public required System.Net.IPAddress ClientIp { get; init; }

        /// <summary>
        /// The user agent hash of the client making the request.
        /// </summary>
        public required string? UserAgentHash { get; init; }

        /// <summary>
        /// The correlation ID for tracing the request.
        /// </summary>
        public required Guid CorrelationId { get; init; }
    }
}
