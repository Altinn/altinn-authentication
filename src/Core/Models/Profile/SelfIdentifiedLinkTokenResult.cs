#nullable enable
using System;

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Outcome of validating a self-identified account-link token (issue #2035). On success it carries
    /// the bound connection parties: <see cref="FromPartyUuid"/> (the SI user being claimed) and
    /// <see cref="ToPartyUuid"/> (the authenticated person who requested the link).
    /// </summary>
    public class SelfIdentifiedLinkTokenResult
    {
        /// <summary>
        /// Gets a value indicating whether the token was valid (signature, issuer, audience, lifetime
        /// and purpose all verified).
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets the party UUID of the self-identified user being claimed (token <c>from_party_uuid</c>),
        /// identified by the username supplied in the request. This is the connection <c>from</c> party
        /// access-management uses, matching what <c>link</c> returns.
        /// </summary>
        public Guid FromPartyUuid { get; init; }

        /// <summary>
        /// Gets the party UUID of the authenticated person who requested the link (token
        /// <c>to_party_uuid</c>) - the connection <c>to</c> party. The redeeming caller must equal this
        /// value.
        /// </summary>
        public Guid ToPartyUuid { get; init; }

        /// <summary>
        /// Gets the token id (<c>jti</c>), for single-use tracking by the caller.
        /// </summary>
        public string? TokenId { get; init; }

        /// <summary>
        /// Creates a failed result. The specific reason is deliberately not carried on the result -
        /// callers respond with a single generic "invalid token" outcome; the reason is logged by the
        /// validator instead.
        /// </summary>
        public static SelfIdentifiedLinkTokenResult Invalid() => new() { IsValid = false };

        /// <summary>
        /// Creates a successful result with the bound from/to party UUIDs and token id.
        /// </summary>
        public static SelfIdentifiedLinkTokenResult Valid(Guid fromPartyUuid, Guid toPartyUuid, string? tokenId) =>
            new() { IsValid = true, FromPartyUuid = fromPartyUuid, ToPartyUuid = toPartyUuid, TokenId = tokenId };
    }
}
