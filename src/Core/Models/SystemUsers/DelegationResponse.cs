namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// The DTO used between the Authtentication Core and the BFF/FrontEnd.
/// We map between this and the AgentDelegationResponseExternal DTO we get
/// in return from the AccessManagement endpoints.
/// </summary>
public class DelegationResponse
{
    public required Guid AgentSystemUserId { get; set; }

    public required Guid DelegationId { get; set; }

    public Guid? ClientUuid { get; set; }       

}

