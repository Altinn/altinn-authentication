namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// DTO to create a new Request for a SystemUser
/// </summary>
/// <param name="ExternalRef">Either just the Orgno for the customer, or a TenantId or other form of disambiguation Id the Vendor needs.</param>
/// <param name="SystemId">The Id for the Registered System that this Request will be based on.</param>
/// <param name="PartyOrgNo">The organisation number for the SystemUser's Party ( the customer that delegates rights to the systemuser) </param>
/// <param name="Rights">The set of Rights requested for this system user. Must be equal to or less than the set defined in the Registered System</param>
/// <param name="RedirectURL">Optional redirect URL to navigate to after the customer has accepted/denied the Request</param>
public class CreateRequestSystemUser()
{ 
    public string ExternalRef { get; set; }
    public string SystemId { get; set; }
    public string PartyOrgNo { get; set; }
    public List<Right> Rights { get; set; }
    public string? RedirectURL { get; set; } 
}

