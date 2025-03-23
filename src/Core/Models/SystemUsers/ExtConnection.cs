using Authorization.Platform.Authorization.Models;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Model lifted from the AM
/// This is the response model from two endpoints
/// </summary>
public class ExtConnection
{
    /// <summary>
    /// Identity, either assignment og delegation
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The entity identity the connection is from (origin, client, source etc) 
    /// </summary>
    public EntityParty From { get; set; }

    /// <summary>
    /// The role To identifies as
    /// </summary>
    public Role Role { get; set; }

    /// <summary>
    /// The entity identity the connection is to (destination, agent, etc)
    /// </summary>
    public EntityParty To { get; set; }

    /// <summary>
    /// The entity betweeen from and to. When connection is delegated.
    /// </summary>
    public EntityParty Facilitator { get; set; }

    /// <summary>
    /// The role the facilitator has to the client 
    /// </summary>
    public Role FacilitatorRole { get; set; }
  
}

/// <summary>
/// Model lifted from AM
/// Only appear in the above ExtConnection in our repo
/// </summary>
public class EntityParty
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string RefId { get; set; }
    public string Type { get; set; }
    public string Variant { get; set; }
}