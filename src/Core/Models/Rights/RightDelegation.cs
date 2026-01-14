using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// Describes the delegation result for a given single right.
    /// </summary>
    public class RightDelegation
    {
        /// <summary>
        /// Specifies who have delegated permissions 
        /// </summary>
        [JsonPropertyName("from")]
        public List<AttributeMatchExternal> From { get; set; } = [];

        /// <summary>
        /// Receiver of the permissions
        /// </summary>
        [JsonPropertyName("to")]
        public List<AttributeMatchExternal> To { get; set; } = [];

        /// <summary>
        /// Specifies the permissions
        /// </summary>
        [JsonPropertyName("resource")]
        public List<AttributeMatchExternal> Resource { get; set; } = [];
    }
}
