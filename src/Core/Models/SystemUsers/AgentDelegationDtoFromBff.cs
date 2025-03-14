namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;
/// <summary>
/// POST Body for the Delegation endpoint used by the FrontEnd BFF 
/// when a Facilitator adds a customer to an existing Agent SystemUser
/// </summary>
public class AgentDelegationDtoFromBff
{
    public required string CustomerId { get; set; }
}
