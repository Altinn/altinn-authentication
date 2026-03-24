namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Resource delegation check response model
/// </summary>
public class Reason
{
    /// <summary>
    /// Description of the reason.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Role ID of the role providing access
    /// </summary>
    public Guid? RoleId { get; set; }

    /// <summary>
    /// Role URN of the role providing access
    /// </summary>
    public string? RoleUrn { get; set; }

    /// <summary>
    /// From party ID of the role providing access
    /// </summary>
    public Guid? FromId { get; set; }

    /// <summary>
    /// Name of the party providing access
    /// </summary>
    public string? FromName { get; set; }

    /// <summary>
    /// To party ID of the role providing access
    /// </summary>
    public Guid? ToId { get; set; }

    /// <summary>
    /// Name of the party the role is providing access to
    /// </summary>
    public string? ToName { get; set; }

    /// <summary>
    /// Via party ID of the keyrole party the user has inherited access through
    /// </summary>
    public Guid? ViaId { get; set; }

    /// <summary>
    /// Name of the party the user has inherited access through
    /// </summary>
    public string? ViaName { get; set; }

    /// <summary>
    /// Role ID for the keyrole the user has for the ViaId party
    /// </summary>
    public Guid? ViaRoleId { get; set; }

    /// <summary>
    /// Role URN for the keyrole the user has for the ViaId party
    /// </summary>
    public string? ViaRoleUrn { get; set; }
}
