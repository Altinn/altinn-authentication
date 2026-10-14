using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// Enum for different right delegation status responses
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DelegationStatusExternal
    {
        /// <summary>
        /// Right was not delegated
        /// </summary>
        NotDelegated = 0,

        /// <summary>
        /// Right was delegated
        /// </summary>
        Delegated = 1
    }
}
