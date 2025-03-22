using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// The return from AccessManagement Delegation API
/// </summary>
[ExcludeFromCodeCoverage]
public class AgentDelegationResponseExternal
{
    /// <summary>
    /// The DelegationId
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The AgentSystemUserId
    /// </summary>
    public Guid FacilitatorId { get; set; }

    public DelegationAssignment From { get; set; }

    public DelegationAssignment To { get; set; }
}

public class DelegationAssignment
{
    /// <summary>
    /// The Assignment Id
    /// </summary>
    public Guid Id { get; set; } 
    
    /// <summary>
    /// The Agent SystemUser Id if this is the right half of an AgentDelegation
    /// </summary>
    public Guid ToId { get; set; }

    /// <summary>
    /// The ClientUid, if this is the left half of an AgentDelegation
    /// </summary>
    public Guid FromId { get; set; }

    /// <summary>
    /// The Guid for the Role in the AM db.
    /// On the left half, this will be the Role that was delegated from the 
    /// Client to the Facilitator.
    /// On the right half, this will be the Agent role that the Faciliator delegated
    /// to the SystemUser.
    /// </summary>
    public Guid RoleId { get; set; }
}


