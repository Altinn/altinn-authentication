using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Input DTO to create a ChangeRequest for an existing SystemUser.
/// There are possibly three sets of Rights regarding a ChangeRequest: 
/// the Required Rights - to be ensured are delegated (idempotent),
/// the Unwanted Rights - to be ensured are NOT delegated (idempotent), 
/// and the Rights that are to be unchanged whether they are delegated or not.
/// The unchanged Rights are not listed explicity in the ChangeRequest.
/// If both the required and unwanted Rights are empty, no new ChangeRequest will be created.
/// A response will be returned with empty Rights lists, indicating no change occured.
/// </summary>
public class ChangeRequestSystemUser()
{
    /// <summary>
    /// Either just the same as the PartyOrgNo for the customer, 
    /// or a TenantId or other form of disambiguation Id,
    /// the Vendor has control over themselves to ensure uniqueness.
    /// A null ExternalRef will default to the PartyOrgNo.
    /// </summary>
    [JsonPropertyName("externalRef")]
    public string? ExternalRef { get; set; }

    /// <summary>
    /// The Id for the Registered System that this Request will be based on. 
    /// Must be owned by the Vendor that creates the Request.
    /// </summary>
    [Required]
    [JsonPropertyName("systemId")]
    public string SystemId { get; set; }

    /// <summary>
    /// The organisation number for the SystemUser's Party 
    /// ( the customer that delegates rights to the systemuser) 
    /// </summary>
    [Required]
    [JsonPropertyName("partyOrgNo")]
    public string PartyOrgNo { get; set; }

    /// <summary>
    /// The set of Rights requested as Required for this system user. 
    /// If already delegated, no change is needed; idempotent.
    /// If not currently delegated, they will be delegated.
    /// Must be equal to or less than the set defined in the Registered System - see SystemId.
    /// An empty list is allowed.
    /// </summary>
    [Required]
    [JsonPropertyName("requiredRights")]
    public List<Right> RequiredRights { get; set; } = [];

    /// <summary>
    /// The set of Rights to be ensured are not delegeted to this system user. 
    /// If currently delegated, they will be revoked.
    /// If already not delegated, no change is needed; idempotent.
    /// An empty list is allowed.
    /// </summary>
    [Required]
    [JsonPropertyName("unwantedRights")]
    public List<Right> UnwantedRights { get; set; } = [];

    /// <summary>
    /// The set of AccessPackages requested as Required for this system user. 
    /// If already delegated, no change is needed; idempotent.
    /// If not currently delegated, they will be delegated.
    /// Must be equal to or less than the set defined in the Registered System - see SystemId.
    /// An empty list is allowed.
    /// </summary>
    [Required]
    [JsonPropertyName("requiredAccessPackagages")]
    public List<AccessPackage> RequiredAccessPackages { get; set; } = [];

    /// <summary>
    /// The set of AccessPackages to be ensured are not delegeted to this system user. 
    /// If currently delegated, they will be revoked.
    /// If already not delegated, no change is needed; idempotent.
    /// An empty list is allowed.
    /// </summary>
    [Required]
    [JsonPropertyName("unwantedAccessPackagages")]
    public List<AccessPackage> UnwantedAccessPackages { get; set; } = [];

    /// <summary>
    /// Optional redirect URL to navigate to after the customer has accepted/denied the Request
    /// </summary>
    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; set; }
}