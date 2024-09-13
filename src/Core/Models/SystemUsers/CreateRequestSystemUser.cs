using System.ComponentModel.DataAnnotations;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// DTO to create a new Request for a SystemUser
/// </summary>
public class CreateRequestSystemUser()
{
    /// <summary>
    /// Either just the same as the PartyOrgNo for the customer, 
    /// or a TenantId or other form of disambiguation Id,
    /// the Vendor has control over themselves to ensure uniqueness.
    /// A null ExternalRef will default to the PartyOrgNo.
    /// </summary>
    public string? ExternalRef { get; set; }

    /// <summary>
    /// The Id for the Registered System that this Request will be based on. 
    /// Must be owned by the Vendor that creates the Request.
    /// </summary>
    [Required]
    public string SystemId { get; set; }

    /// <summary>
    /// The organisation number for the SystemUser's Party 
    /// ( the customer that delegates rights to the systemuser) 
    /// </summary>
    [Required]
    public string PartyOrgNo { get; set; }

    /// <summary>
    /// The set of Rights requested for this system user. 
    /// Must be equal to or less than the set defined in the Registered System.
    /// An empty list will throw an error.
    /// </summary>
    [Required]
    public List<Right> Rights { get; set; }

    /// <summary>
    /// Optional redirect URL to navigate to after the customer has accepted/denied the Request
    /// </summary>
    public string? RedirectURL { get; set; }
}