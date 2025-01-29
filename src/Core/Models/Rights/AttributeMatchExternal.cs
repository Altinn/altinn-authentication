using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// This model describes a pair of AttributeId and AttributeValue for use in matching in XACML policies, for instance a resource, a user, a party or an action.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AttributeMatchExternal
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
