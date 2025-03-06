﻿using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Response DTO through the API after Vendor calls to Create a new Request for a ClientSystemUser the first time,
/// or in other status API until and after approval.
/// </summary>
public class ClientRequestSystemResponse()    
{
    /// <summary>
    /// Guid in the format of a UUID-v4 Id generated by us; also will be reused by the SystemUser when it is approved.
    /// We use a Guid rather than the External Request Id, to facilitate Db administration and the possiblity to delete and renew a SystemUser.
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Either just the Orgno for the customer, or a TenantId or other form of disambiguation Id the Vendor needs.
    /// Is one of the three parts of the External Request Id.
    /// A blank ExternalRef will be overwritten as a copy of the Cutomer's OrgNo
    /// </summary>    
    [JsonPropertyName("externalRef")]
    public string? ExternalRef { get; set; }

    /// <summary>
    /// The Id for the Registered System that this Request will be based on.
    /// Is one of the three parts of the External Request Id.
    /// </summary>
    [Required]
    [JsonPropertyName("systemId")]
    public string SystemId { get; set; }

    /// <summary>
    /// The organisation number for the SystemUser's Party ( the customer that delegates rights to the systemuser).
    /// Is one of the three parts of the External Request Id.
    /// </summary>
    [Required]
    [JsonPropertyName("partyOrgNo")]
    public string PartyOrgNo { get; set; }

    /// <summary>
    /// The set of Rights requested for this system user. Must be equal to or less than the set defined in the Registered System.
    /// Must be a minimum of 1 selected Right.
    /// </summary>
    [Required]
    [JsonPropertyName("accessPackages")]
    public List<AccessPackage> AccessPackages { get; set; }

    /// <summary>
    /// Initially the request is "new", 
    /// other values are "accepted", "rejected", "denied"
    /// </summary>
    [Required]
    [JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Optional redirect URL to navigate to after the customer has accepted/denied the Request
    /// </summary>    
    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// The URL the Vendor can direct their customer to in the Authn.UI to Approve or Reject the Request for the new SystemUser
    /// Contains the base url for the Authn.UI + the Request.Id
    /// <host>/authfront/ui/auth/vendorrequest?id=<requestGuid>
    /// </summary>
    [JsonPropertyName("confirmUrl")]
    public string? ConfirmUrl { get; set; }

    /// <summary>
    /// The date and time the Request was created,
    /// used to determine if the Request is still valid.
    /// </summary>
    [JsonIgnore]
    public DateTime Created { get; set; }

    /// <summary>
    /// The system user type
    /// </summary>
    [JsonIgnore]
    public SystemUserType UserType { get; set; }
}
