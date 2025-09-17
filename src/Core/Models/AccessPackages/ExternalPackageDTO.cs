namespace Altinn.Platform.Authentication.Core.Models.AccessPackages;

/// <summary>
/// Is a DTO from AccessManagement's API. 
/// See https://github.com/Altinn/altinn-authorization-tmp/blob/main/src/apps/Altinn.AccessManagement/src/Altinn.AccessMgmt.Persistence/Services/Models/PackagePermission.cs
/// Wraps the AccessPackages in a Compact Format
/// </summary>
public class ExternalPackageDTO
{
    /// <summary>
    /// Package the permissions are for
    /// </summary>
    public required CompactPackage Package { get; set; }
 
}
