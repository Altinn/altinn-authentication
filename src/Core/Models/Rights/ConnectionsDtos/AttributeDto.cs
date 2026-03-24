using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// This model describes a an Attribute consisting of an Attribute Type and Attribute Value which can also be represented as a Urn by combining the properties as '{type}:{value}'
/// It's used both for external API input/output but also internally for working with attributes and matching to XACML-attributes used in policies, indentifying for instance a resource, a user, a party or an action.
/// </summary>
public class AttributeDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeDto"/> class.
    /// </summary>
    public AttributeDto()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeDto"/> class.
    /// </summary>
    public AttributeDto(string type, string value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the attribute id for the match
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the attribute value for the match
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }

    /// <summary>
    /// returns the type and value as a urn in the format '{type}:{value}'
    /// </summary>
    public string Urn()
    {
        return $"{Type}:{Value}";
    }
}