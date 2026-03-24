namespace Altinn.Platform.Authentication.Core.Models.Rights;
/// <summary>
/// Resource permissions
/// </summary>
public class ResourcePermissionDto
{
    /// <summary>
    /// Resource the permissions are for
    /// </summary>
    public ResourceDto Resource { get; set; }

    /// <summary>
    /// Parties with permissions
    /// </summary>
    public IEnumerable<PermissionDto> Permissions { get; set; }
}

