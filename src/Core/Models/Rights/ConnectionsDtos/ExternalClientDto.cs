using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

/// <summary>
/// Used to represent a connected client party in an Agent-SystemUser Client-Delegation context,
/// sent to external SystemUserClientDelegation API users, or the BFF/AM.UI
/// </summary>
[ExcludeFromCodeCoverage]
public class ExternalClientDto
{
    /// <summary>
    /// UUid of the party
    /// </summary>
    public required Guid PartyUuid { get; set; }

    /// <summary>
    /// Display name of the party
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Organisation number
    /// </summary>
    public required string OrganizationIdentifier { get; set; }

    /// <summary>
    /// Gets or sets a collection of all access information for the client 
    /// </summary>
    public List<RoleAccessPackagesPrimitive> Access { get; set; } = [];
}
