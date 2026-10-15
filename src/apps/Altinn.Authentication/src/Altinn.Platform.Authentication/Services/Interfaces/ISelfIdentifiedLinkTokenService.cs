#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Mints and validates the short-lived, single-purpose token used to link a self-identified (SI)
    /// user account to an authenticated person in the forgot-password / account-claim flow (issue #2035).
    /// </summary>
    /// <remarks>
    /// The token is signed with a dedicated certificate (see
    /// <see cref="ISelfIdentifiedLinkTokenCertificateProvider"/>) and is <b>not</b> an authentication
    /// token: it grants nothing on its own and only asserts the bound <c>source</c>/<c>target</c> user
    /// pair to the redeeming access-management flow.
    /// </remarks>
    public interface ISelfIdentifiedLinkTokenService
    {
        /// <summary>
        /// Mints a signed link token binding the self-identified user being claimed
        /// (<paramref name="fromPartyUuid"/>, the connection <c>from</c> party) to the authenticated
        /// person who triggered the request (<paramref name="toPartyUuid"/>, the connection <c>to</c> party).
        /// </summary>
        Task<string> MintAsync(Guid fromPartyUuid, Guid toPartyUuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a link token (signature, issuer, audience, lifetime and purpose) and returns the
        /// bound source/target user ids. Never throws on an invalid token - returns an invalid result.
        /// </summary>
        Task<SelfIdentifiedLinkTokenResult> ValidateAsync(string? token, CancellationToken cancellationToken = default);
    }
}
