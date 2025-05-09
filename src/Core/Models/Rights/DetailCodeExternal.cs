using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Fixed values for DetailCodes
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DetailCodeExternal
{
    /// <summary>
    /// Unknown reason should not be used
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
    /// The service requires explicit access in SRR and the reportee has this
    /// </summary>
    SrrRightAccess = 3,

    /// <summary>
    /// Has not access by a delegation of role in ER or Altinn
    /// </summary>
    MissingRoleAccess = 4,

    /// <summary>
    /// Has not access by direct delegation
    /// </summary>
    MissingDelegationAccess = 5,

    /// <summary>
    /// The service requires explicit access in SRR and the reportee is missing this
    /// </summary>
    MissingSrrRightAccess = 6,

    /// <summary>
    /// The service requires explicit authentication level and the reportee is missing this
    /// </summary>
    InsufficientAuthenticationLevel = 7,

    /// <summary>
    /// The receiver already has the right
    /// </summary>
    AlreadyDelegated = 8,

    /// <summary>
    ///  The receiver has the right based on Access List delegation
    /// </summary>
    AccessListValidationPass = 9,

    /// <summary>
    ///  The receiver does not have the right based on Access List delegation
    /// </summary>
    AccessListValidationFail = 10,
}