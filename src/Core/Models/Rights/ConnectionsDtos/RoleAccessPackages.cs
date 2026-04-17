using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

/// <summary>
/// DTO used for delegegation.Needs to deserialize child objects, 
/// see also the primitive version if those are not available.
/// </summary>
[ExcludeFromCodeCoverage]
public class RoleAccessPackages
{
    /// <summary>
    /// Roles
    /// </summary>
    [JsonPropertyName("role")]
    public CompactRoleDto Role { get; set; }

    /// <summary>
    /// Packages
    /// </summary>
    [JsonPropertyName("packages")]
    public CompactPackageDto[] Packages { get; set; }
}


/// <summary>
/// The string primitive version of RoleAccessPackages, 
/// used for the batch delegation endpoint, 
/// </summary>
[ExcludeFromCodeCoverage]
public class RoleAccessPackagesPrimitive
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("packages")]
    public List<string> Packages { get; set; }
}