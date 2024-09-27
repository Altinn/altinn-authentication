using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// This model describes a pair of AttributeId and AttributeValue for use in matching in XACML policies, 
    /// for instance a resource, a user, a party or an action.
    /// </summary>
    public class AttributePair
    {
        /// <summary>
        /// Gets or sets the attribute id for the match
        /// </summary>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the attribute value for the match
        /// </summary>
        [Required]
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }
}
