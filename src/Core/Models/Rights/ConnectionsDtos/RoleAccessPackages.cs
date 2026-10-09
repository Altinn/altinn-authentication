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
    public CompactRoleDto Role { get; set; } = null!; // an access entry from Access Management always carries its role

    /// <summary>
    /// Packages
    /// </summary>
    [JsonPropertyName("packages")]
    public CompactPackageDto[] Packages { get; set; } = []; // an access entry from Access Management always carries its packages
}


/// <summary>
/// The string primitive version of RoleAccessPackages, 
/// used for the batch delegation endpoint, 
/// </summary>
[ExcludeFromCodeCoverage]
public class RoleAccessPackagesPrimitive
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("packages")]
    public required List<string> Packages { get; set; }
}