using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Compact Role Model
/// </summary>
[ExcludeFromCodeCoverage]
public class CompactRoleDto
{
    /// <summary>
    /// Id
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Value
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; }

    /// <summary>
    /// Value
    /// </summary>
    [JsonPropertyName("urn")]
    public string Urn { get; set; }

    /// <summary>
    /// Value
    /// </summary>
    [JsonPropertyName("legacyurn ")]
    public string LegacyUrn { get; set; }

    /// <summary>
    /// Children
    /// </summary>
    [JsonPropertyName("children")]
    public List<CompactRoleDto> Children { get; set; }
}
