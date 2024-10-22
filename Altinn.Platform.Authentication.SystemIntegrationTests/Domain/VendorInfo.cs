using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain
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
