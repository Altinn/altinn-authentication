#nullable enable
using System;

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Outcome of validating a self-identified account-link token (issue #2035). On success it carries
    /// the bound <see cref="SourceUserId"/> (the authenticated person who requested the link) and
    /// <see cref="TargetPartyUuid"/> (the party UUID of the SI user being claimed).
    /// </summary>
    public class SelfIdentifiedLinkTokenResult
    {
        /// <summary>
        /// Gets a value indicating whether the token was valid (signature, issuer, audience, lifetime
        /// and purpose all verified).
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets the authenticated person who requested the link (token <c>source_user_id</c>). The
        /// redeeming caller must equal this value.
        /// </summary>
        public int SourceUserId { get; init; }

        /// <summary>
        /// Gets the party UUID of the self-identified user being claimed (token
        /// <c>target_party_uuid</c>). This is the value access-management uses as the connection
        /// source, matching what <c>validate-credentials</c> returns.
        /// </summary>
        public Guid TargetPartyUuid { get; init; }

        /// <summary>
        /// Gets the token id (<c>jti</c>), for single-use tracking by the caller.
        /// </summary>
        public string? TokenId { get; init; }

        /// <summary>
        /// Gets a short, non-sensitive reason when <see cref="IsValid"/> is <c>false</c>.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Creates a failed result with the given reason.
        /// </summary>
        public static SelfIdentifiedLinkTokenResult Invalid(string error) => new() { IsValid = false, Error = error };

        /// <summary>
        /// Creates a successful result with the bound source user id, target party UUID and token id.
        /// </summary>
        public static SelfIdentifiedLinkTokenResult Valid(int sourceUserId, Guid targetPartyUuid, string? tokenId) =>
            new() { IsValid = true, SourceUserId = sourceUserId, TargetPartyUuid = targetPartyUuid, TokenId = tokenId };
    }
}
