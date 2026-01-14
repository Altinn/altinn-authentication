using Altinn.Platform.Authentication.Core.Models.AccessPackages;
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
    /// The set of Rights requested as Required for this system user. 
    /// If already delegated, no change is needed; idempotent.
    /// If not currently delegated, they will be delegated.
    /// Must be equal to or less than the set defined in the Registered System - see SystemId.
    /// An empty list is allowed.
    /// </summary>
    [JsonPropertyName("requiredRights")]
    public List<Right> RequiredRights { get; set; } = [];

    /// <summary>
    /// The set of Rights to be ensured are not delegeted to this system user. 
    /// If currently delegated, they will be revoked.
    /// If already not delegated, no change is needed; idempotent.
    /// An empty list is allowed.
    /// </summary>
    [JsonPropertyName("unwantedRights")]
    public List<Right> UnwantedRights { get; set; } = [];

    /// <summary>
    /// The set of AccessPackages requested as Required for this system user. 
    /// If already delegated, no change is needed; idempotent.
    /// If not currently delegated, they will be delegated.
    /// Must be equal to or less than the set defined in the Registered System - see SystemId.
    /// An empty list is allowed.
    /// </summary>
    [JsonPropertyName("requiredAccessPackages")]
    public List<AccessPackage> RequiredAccessPackages { get; set; } = [];

    /// <summary>
    /// The set of AccessPackages to be ensured are not delegated to this system user. 
    /// If currently delegated, they will be revoked.
    /// If already not delegated, no change is needed; idempotent.
    /// An empty list is allowed.
    /// </summary>
    [JsonPropertyName("unwantedAccessPackages")]
    public List<AccessPackage> UnwantedAccessPackages { get; set; } = [];

    /// <summary>
    /// Optional redirect URL to navigate to after the customer has accepted/denied the Request
    /// </summary>
    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; set; }
}