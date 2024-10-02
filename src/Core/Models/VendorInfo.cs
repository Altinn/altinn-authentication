using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models
{
    /// summary
    /// Organization claim matching structure from maskinporten
    /// /summary
    public class VendorInfo
    {
        /// summary
        /// The authority that defines organization numbers. 
        /// /summary
        [JsonPropertyName("authority")]
        public string Authority;

        [JsonPropertyName("ID")]
        public string ID { get; set; }
    }
}
