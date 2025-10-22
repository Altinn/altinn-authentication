using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the properties required to create an unregistered client request.
    /// </summary>
    public sealed class UnregisteredClientRequestCreate
    {
        /// <summary>
        /// The unique identifier for the request.
        /// </summary>
        public Guid RequestId { get; init; }

        /// <summary>
        /// When the request expires.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// The optional issuer of the unregistered client.
        /// </summary>
        public string? Issuer {  get; init; }

        /// <summary>
        /// Defines the GoTo url where the user should be redirected after the request is processed.
        /// </summary>
        public string? GotoUrl {  get; init; }

        /// <summary>
        /// Defines the IP adress of the creator of the unregistered client request.
        /// </summary>
        public IPAddress? CreatedByIp { get; init; }

        /// <summary>
        /// The user agent hash of the creator of the unregistered client request.
        /// </summary>
        public string? UserAgentHash { get; set; }

        /// <summary>
        /// Correlation ID to trace the request. Used for logging and tracking purposes.
        /// </summary>
        public Guid? CorrelationId { get; init; }
    }
}
