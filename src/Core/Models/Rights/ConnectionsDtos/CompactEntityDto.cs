using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Compact Entity Model
/// </summary>
public class CompactEntityDto
{
    /// <summary>
    /// Id
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Variant
    /// </summary>
    [JsonPropertyName("variant")]
    public string Variant { get; set; }

    /// <summary>
    /// Parent
    /// </summary>
    [JsonPropertyName("parent")]
    public CompactEntityDto Parent { get; set; }

    /// <summary>
    /// Children
    /// </summary>
    [JsonPropertyName("children")]
    public List<CompactEntityDto> Children { get; set; }

    /// <summary>
    /// PartyId
    /// </summary>
    [JsonPropertyName("partyid")]
    public int? PartyId { get; set; }

    /// <summary>
    /// UserId
    /// </summary>
    [JsonPropertyName("userId")]
    public int? UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// OrganizationIdentifier
    /// </summary>
    [JsonPropertyName("organizationIdentifier")]
    public string? OrganizationIdentifier { get; set; }

    /// <summary>
    /// PersonIdentifier
    /// </summary>
    [JsonPropertyName("personIdentifier")]
    public string? PersonIdentifier { get; set; }

    /// <summary>
    /// DateOfBirth
    /// </summary>
    [JsonPropertyName("dateOfBirth")]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// DateOfDeath
    /// </summary>
    [JsonPropertyName("dateOfDeath")]
    public DateOnly? DateOfDeath { get; set; }

    /// <summary>
    /// IsDeleted
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// DeletedAt
    /// </summary>
    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }
}
