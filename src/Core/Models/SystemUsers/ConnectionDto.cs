using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Model lifted from the AM
/// This is the response model from two endpoints
/// </summary>
[ExcludeFromCodeCoverage]
public class ConnectionDto
{
    /// <summary>
    /// Identity, either assignment og delegation
    /// </summary>
    public Guid Id { get; set; } 

    /// <summary>
    /// Delegation if connection is delegated
    /// </summary>
    public Delegation Delegation { get; set; }

    /// <summary>
    /// The entity identity the connection is from (origin, client, source etc) 
    /// </summary>
    public EntityParty From { get; set; } 

    /// <summary>
    /// The role To identifies as
    /// </summary>
    public RoleDto Role { get; set; } 

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
    public RoleDto FacilitatorRole { get; set; } 
  
}

/// <summary>
/// Model lifted from the AM
/// This is the response model from two endpoints
/// </summary>
[ExcludeFromCodeCoverage]
public class AgentDelegationResponse
{
    /// <summary>
    /// Delegationidentifier
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Client party identifier
    /// </summary>
    public Guid FromEntityId { get; set; }
}

/// <summary>
/// Delegation between two assignments
/// </summary>
public class Delegation
{
    /// <summary>
    /// Identity
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Assignment to delegate from
    /// </summary>
    public Guid FromId { get; set; }

    /// <summary>
    /// Assignment to delegate to
    /// </summary>
    public Guid ToId { get; set; }

    /// <summary>
    /// Entity owner of the Delegation
    /// </summary>
    public Guid FacilitatorId { get; set; }
}

/// <summary>
/// Model lifted from AM
/// Only appear in the above ExtConnection in our repo
/// </summary>
[ExcludeFromCodeCoverage]
public class EntityParty
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string RefId { get; set; }
    public string Type { get; set; }
    public string Variant { get; set; }
}

/// <summary>
/// Role between entities for creating Assignments
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name
    /// e.g Dagligleder
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Code
    /// e.g daglig-leder
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Description
    /// e.g The main operator of the organization
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Defines the role as a KeyRole
    /// </summary>
    public bool IsKeyRole { get; set; }

    /// <summary>
    /// Urn
    /// e.g altinn:external-role:ccr:daglig-leder
    /// </summary>
    public string Urn { get; set; }
}