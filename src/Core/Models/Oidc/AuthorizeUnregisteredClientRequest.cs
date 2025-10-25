using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines an open authorize request without client_id. Typically requested by Altinn Apps or other applications in Altinn Platform that do not have a client_id.
    /// </summary>
    public sealed class AuthorizeUnregisteredClientRequest
    {
        /// <summary>
        /// Defines the GoTo parameter sendt in the authorize request.
        /// </summary>
        public string? GoTo { get; init; }

        /// <summary>
        /// If set, defines the requested issuer to be used for the authentication.
        /// </summary>
        public string? RequestedIss { get; init; }

        /// <summary>
        /// If set, defines the requested acr values to be used for the authentication.
        /// </summary>
        public string[]? AcrValues { get; init; }

        /// <summary>
        /// The client IP address of the request. Used for transaction logging.
        /// </summary>
        public IPAddress? ClientIp { get; init; }

        /// <summary>
        /// Defines a hash of the user agent string of the client application. Used for device recognition.
        /// </summary>
        public string? UserAgentHash { get; init; }

        /// <summary>
        /// Defines an optional correlation id to be used for tracing the request.
        /// </summary>
        public Guid? CorrelationId { get; init; }
    }
}
