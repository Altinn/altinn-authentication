#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Orchestrates the self-identified account-link "request email" step (issue #2035): look up the
    /// SI user by username, mint the link token, and send the email to the SI account's stored address.
    /// </summary>
    public interface ISelfIdentifiedLinkService
    {
        /// <summary>
        /// Requests an account-link email for the SI user identified by <paramref name="userName"/>,
        /// to be sent to that user's stored email. When the user is unknown, inactive or has no email,
        /// this is a silent no-op (so the caller can respond identically and avoid user enumeration).
        /// </summary>
        /// <param name="userName">The SI user's username (the connection <c>from</c> party).</param>
        /// <param name="toPartyUuid">
        /// The authenticated person who triggered the request (the connection <c>to</c> party).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RequestLinkAsync(string userName, Guid toPartyUuid, CancellationToken cancellationToken = default);
    }
}
