using System.Runtime.Serialization;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// The consumer claim object
    /// </summary>
    public class ConsumerClaim
    {
        /// <summary>
        /// Gets or sets the format of the identifier. Must always be "iso6523-actorid-upis"
        /// </summary>
        [DataMember(Name = "authority")]
        public string Authority { get; set; }

        /// <summary>
        /// Gets or sets the identifier for the consumer. Must have ISO6523 prefix, which should be "0192:" for norwegian organization numbers
        /// </summary>
        [DataMember(Name = "ID")]
        public string Id { get; set; }
    }
}
