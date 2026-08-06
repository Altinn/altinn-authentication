using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

/// <summary>
/// Compact Package Model
/// </summary>
[ExcludeFromCodeCoverage]
public class CompactPackageDto
{
    /// <summary>
    /// Id
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Urn
    /// </summary>
    [JsonPropertyName("urn")]
    public string Urn { get; set; }

    /// <summary>
    /// AreaId
    /// </summary>
    [JsonPropertyName("areaId")]
    public Guid AreaId { get; set; }
}
