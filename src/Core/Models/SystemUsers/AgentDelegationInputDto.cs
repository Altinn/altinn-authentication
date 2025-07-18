﻿using System.Diagnostics.CodeAnalysis;
using static Altinn.Platform.Authentication.Core.Models.SystemUsers.ClientDto;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;
/// <summary>
/// POST Body for the Delegation endpoint sent by the FrontEnd BFF 
/// when a Facilitator adds a customer to an existing Agent SystemUser
/// </summary>
[ExcludeFromCodeCoverage]
public class AgentDelegationInputDto
{
    /// <summary>
    /// The Uuid for the client/customer of the Facilitator, 
    /// which will be assigned to the Agent SystemUser    
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// The Uuid for the facilitator, the organisation 
    /// or person that "owns" the Agent SystemUser
    /// and is administrating it from the FrontEnd.
    /// </summary>
    public required string FacilitatorId { get; set; }

    /// <summary>
    /// Gets or sets a collection of all access information for the client 
    /// </summary>
    public List<ClientRoleAccessPackages> Access { get; set; } = [];
}
