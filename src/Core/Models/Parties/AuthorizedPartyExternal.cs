namespace Altinn.Platform.Authentication.Core.Models;

#nullable enable
public  class AuthorizedPartyExternal
{
    /// <summary>
    /// Gets or sets the universally unique identifier of the party
    /// </summary>
    public Guid PartyUuid { get; set; }

    /// <summary>
    /// Gets or sets the name of the party
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the organization number if the party is an organization
    /// </summary>
    public string OrganizationNumber { get; set; }

    /// <summary>
    /// Gets the national identity number if the party is a person
    /// </summary>
    public string PersonId { get; set; }

    /// <summary>
    /// Gets or sets the party id
    /// </summary>
    public int PartyId { get; set; }

    /// <summary>
    /// Gets or sets the type of party
    /// </summary>
    public AuthorizedPartyTypeExternal Type { get; set; }

    /// <summary>
    /// Gets or sets the unit type if the party is an organization
    /// </summary>
    public string UnitType { get; set; }

    /// <summary>
    /// Gets or sets whether this party is marked as deleted in the Central Coordinating Register for Legal Entities
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the party is only included as a hierarchy element without any access. Meaning a main unit where the authorized subject only have access to one or more of the subunits.
    /// </summary>
    public bool OnlyHierarchyElementWithNoAccess { get; set; }

    /// <summary>
    /// Gets or sets a collection of all resource identifier the authorized subject has some access to on behalf of this party
    /// </summary>
    public List<string> AuthorizedResources { get; set; } = [];

    /// <summary>
    /// Gets or sets a collection of all rolecodes for roles from either Enhetsregisteret or Altinn 2 which the authorized subject has been authorized for on behalf of this party
    /// </summary>
    public List<string> AuthorizedRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets a set of subunits of this party, which the authorized subject also has some access to.
    /// </summary>
    public List<AuthorizedPartyExternal> Subunits { get; set; } = [];
}
