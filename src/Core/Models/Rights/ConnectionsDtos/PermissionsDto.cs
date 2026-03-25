using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights;
/// <summary>
/// Permission
/// </summary>
[ExcludeFromCodeCoverage]
public class PermissionDto
{
    /// <summary>
    /// From party
    /// </summary>
    public CompactEntityDto From { get; set; }

    /// <summary>
    /// To party
    /// </summary>
    public CompactEntityDto To { get; set; }

    /// <summary>
    /// Via party
    /// </summary>
    public CompactEntityDto Via { get; set; }

    /// <summary>
    /// Role
    /// </summary>
    public CompactRoleDto Role { get; set; }

    /// <summary>
    /// Via role
    /// </summary>
    public CompactRoleDto ViaRole { get; set; }

    /// <summary>
    /// Reason
    /// </summary>
    public AccessReason Reason { get; set; }
}
