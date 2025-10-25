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
        /// The Altinn2 token based on .net properitary encryption standard. Will be validated against Altinn2.
        /// </summary>
        public required string EncryptedTicket { get; set; }
        public IPAddress CreatedByIp { get; set; }
        public string? UserAgentHash { get; set; }
        public Guid CorrelationId { get; set; }
    }
}
