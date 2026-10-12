using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// Organization claim matching structure from maskinporten
    /// </summary>
    public class VendorInfo
    {
        /// <summary>
        /// The organisation number of the vendor, prefixed with its ISO 6523 authority
        /// (for instance <c>0192:991825827</c> for a Norwegian organisation number).
        /// </summary>
        [JsonPropertyName("ID")]
        public required string ID { get; set; }
    }
}
