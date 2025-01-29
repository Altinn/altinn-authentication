using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// Response model describing the delegation result for a given single right, whether the authenticated user was able to delegate the right or not on behalf of the from part.
    /// </summary>
    public class RightDelegationResultExternal
    {
        /// <summary>
        /// Gets or sets the right key
        /// </summary>
        [Required]
        public string RightKey { get; set; }

        /// <summary>
        /// Gets or sets the set of Attribute Id and Attribute Value for identifying the resource which the right provides access to
        /// </summary>
        [Required]
        public List<AttributeMatchExternal> Resource { get; set; }

        /// <summary>
        /// Gets or sets the action the right gives access to perform on the resource
        /// </summary>
        [Required]
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the right was successfully delegated or not
        /// </summary>
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DelegationStatusExternal Status { get; set; }

        /// <summary>
        /// Gets or sets a list of details describing the reason(s) behind the status (if any can be provided)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DetailExternal> Details { get; set; }
    }
}
