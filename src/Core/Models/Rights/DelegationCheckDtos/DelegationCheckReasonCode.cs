using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelegationCheckReasonCode
{
    /// <summary>
    /// Unknown reason
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Has access by a delegated role in ER or Altinn 
    /// </summary>
    RoleAccess = 1,

    /// <summary>
    /// Has access by direct delegation
    /// </summary>
    DelegationAccess = 2,

    /// <summary>
    /// Has not access by a delegation of role in ER or Altinn
    /// </summary>
    MissingRoleAccess = 4,

    /// <summary>
    /// Has not access by direct delegation
    /// </summary>
    MissingDelegationAccess = 5,

    /// <summary>
    ///  The receiver does not have the right based on Access List delegation
    /// </summary>
    AccessListValidationFail = 10,

    /// <summary>
    /// Has access by a delegated package in Altinn 
    /// </summary>
    PackageAccess = 11,

    /// <summary>
    /// Has not access by a delegation of package in Altinn
    /// </summary>
    MissingPackageAccess = 12,

    /// <summary>
    /// Indicates that the resource cannot be delegated to another user or entity.
    /// </summary>
    ResourceNotDelegable = 13,

    /// <summary>
    /// Resource is not delegable in ConnectionApi due to being a maskinportenschema resource.
    /// </summary>
    ResourceIsMaskinPortenSchema = 14
}
