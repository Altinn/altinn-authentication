#nullable enable
using System;

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// The pieces of a migrated self-identified (SI) user credential needed to start the account-link
    /// flow (issue #2035): the SI user's party UUID (the connection <c>from</c> party) and the stored
    /// email the link is sent to.
    /// </summary>
    public class SelfIdentifiedLinkTarget
    {
        /// <summary>
        /// Gets the SI user's party UUID - becomes <c>from_party_uuid</c> in the link token.
        /// </summary>
        public required Guid PartyUuid { get; init; }

        /// <summary>
        /// Gets the SI user's stored email - the address the link is sent to. Always non-empty when a
        /// target is returned (a missing email yields no target at all).
        /// </summary>
        public required string Email { get; init; }
    }
}
