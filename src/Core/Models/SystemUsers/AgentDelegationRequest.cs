using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Input from the FrontEnd/BFF and sent to AccessManagement to Delegate the Faciliator's Customer to the Agent SystemUser
/// </summary>
[ExcludeFromCodeCoverage]
public class AgentDelegationRequest
{
    public required Guid ClientId { get; set; } // Customer (PartyUuid)
    public required Guid FacilitatorId { get; set; } // SystemFacilitator (PartyUuid)
    public required string AgentName { get; set; } // SystemUser DisplayName
    public required Guid AgentId { get; set; } // SystemUser Id (PartyUuid)
    public required string AgentRole { get; set; } // AGENT // evt ny std "daglig-leder"
    public List<AgentDelegationDetails> Delegations { get; set; } = [];// Possibly several accesspackages are delegated
}

