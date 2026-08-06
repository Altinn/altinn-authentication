using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

[ExcludeFromCodeCoverage]
public class DelegationDto
{
    /// <summary>
    /// PackageId
    /// </summary>
    [JsonPropertyName("roleId")]
    public Guid RoleId { get; set; }

    /// <summary>
    /// PackageId
    /// </summary>
    [JsonPropertyName("packageId")]
    public Guid PackageId { get; set; }

    /// <summary>
    /// PackageId
    /// </summary>
    [JsonPropertyName("viaId")]
    public Guid ViaId { get; set; }

    /// <summary>
    /// FromId
    /// </summary>
    [JsonPropertyName("fromId")]
    public Guid FromId { get; set; }

    /// <summary>
    /// ToId
    /// </summary>
    [JsonPropertyName("toId")]
    public Guid ToId { get; set; }

    /// <summary>
    /// Indicates whether the request resulted in any state change.
    /// </summary>
    [JsonPropertyName("changed")]
    public bool Changed { get; set; }
}
