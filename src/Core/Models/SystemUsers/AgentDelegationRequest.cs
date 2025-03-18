using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Input from the FrontEnd/BFF and sent to AccessManagement to Delegate the Faciliator's Customer to the Agent SystemUser
/// </summary>
[ExcludeFromCodeCoverage]
public class AgentDelegationRequest
{
    /// <summary>
    /// The Uuid for the client/customer of the Facilitator, 
    /// which will be assigned to the Agent SystemUser    
    /// </summary>
    public required Guid ClientId { get; set; }

    /// <summary>
    /// The Uuid for the facilitator, the organisation 
    /// or person that "owns" the Agent SystemUser
    /// and is administrating it from the FrontEnd.
    /// </summary>
    public required Guid FacilitatorId { get; set; } 

    /// <summary>
    /// The chosen display name for the Agent SystemUser
    /// </summary>
    public required string AgentName { get; set; } // SystemUser DisplayName

    /// <summary>
    /// Agent SystemUser Id (PartyUuid)
    /// </summary>
    public required Guid AgentId { get; set; } 

    /// <summary>
    /// The Agent SystemUser always has the Role AGENT in this context
    /// </summary>
    public required string AgentRole { get; set; } 

    /// <summary>
    /// One or more AccessPackages are to be delegated, each with an 
    /// inheritance from a specific Role.
    /// </summary>
    public List<AgentDelegationDetails> Delegations { get; set; } = [];
}

