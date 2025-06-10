namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    /// <summary>
    /// A database record for a party.
    /// </summary>
    public record PartyRecord
    {
        /// <summary>
        /// Gets the UUID of the party.
        /// </summary>
        public required Guid PartyUuid { get; init; }

        /// <summary>
        /// Gets the ID of the party.
        /// </summary>
        public required int PartyId { get; init; }

        /// <summary>
        /// Gets the display-name of the party.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
        /// </summary>
        public required string OrganizationIdentifier { get; init; }
    }
}