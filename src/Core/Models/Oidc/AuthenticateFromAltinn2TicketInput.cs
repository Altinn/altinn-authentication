using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// DTO for input when authenticating from an Altinn2 token session.
    /// The Altinn2 token is expected to be a valid token issued by Altinn2.
    /// </summary>
    public class AuthenticateFromAltinn2TicketInput
    {
        /// <summary>
        /// The Altinn2 token based on .NET proprietary encryption standard. Will be validated against Altinn2.
        /// </summary>
        public required string EncryptedTicket { get; set; }

        /// <summary>
        /// The IP address of the client creating the authentication request.
        /// </summary>
        public required IPAddress CreatedByIp { get; set; }

        /// <summary>
        /// The hash of the user agent of the client creating the authentication request.
        /// </summary>
        public string? UserAgentHash { get; set; }

        /// <summary>
        /// Correlation id for tracking the request.
        /// </summary>
        public Guid CorrelationId { get; set; }
    }
}
