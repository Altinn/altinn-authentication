#nullable enable
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
        /// Mints a signed link token binding the authenticated requester (<paramref name="sourceUserId"/>)
        /// to the self-identified user being claimed (<paramref name="targetUserId"/>).
        /// </summary>
        Task<string> MintAsync(int sourceUserId, int targetUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a link token (signature, issuer, audience, lifetime and purpose) and returns the
        /// bound source/target user ids. Never throws on an invalid token - returns an invalid result.
        /// </summary>
        Task<SelfIdentifiedLinkTokenResult> ValidateAsync(string token, CancellationToken cancellationToken = default);
    }
}
