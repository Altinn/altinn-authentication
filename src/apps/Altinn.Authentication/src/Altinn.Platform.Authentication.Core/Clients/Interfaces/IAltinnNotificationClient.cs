#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Authentication.Core.Clients.Interfaces
{
    /// <summary>
    /// Client for the Altinn Notifications platform API. Used to send the self-identified
    /// account-link email (issue #2035) directly to a known email address.
    /// </summary>
    public interface IAltinnNotificationClient
    {
        /// <summary>
        /// Sends an email notification to a specific address (no contact-register lookup), for
        /// immediate delivery. Returns <c>true</c> when the order was accepted by the Notifications
        /// service; <c>false</c> on transport/validation failure (logged, never throwing).
        /// </summary>
        /// <param name="emailAddress">The recipient email address.</param>
        /// <param name="subject">The email subject.</param>
        /// <param name="htmlBody">The email body (HTML).</param>
        /// <param name="idempotencyId">Sender-defined idempotency id, used to de-duplicate retries.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<bool> SendEmailAsync(
            string emailAddress,
            string subject,
            string htmlBody,
            string idempotencyId,
            CancellationToken cancellationToken = default);
    }
}
