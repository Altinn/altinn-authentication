using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Profile.Enums
{
    /// <summary>
    /// The self-identified user type. Mirrors register's
    /// <c>Altinn.Register.Contracts.SelfIdentifiedUserType</c> wire shape so payloads round-trip
    /// across the <c>POST /register/api/v2/internal/users/self-identified</c> contract.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelfIdentifiedUserType>))]
    public enum SelfIdentifiedUserType
    {
        /// <summary>
        /// Legacy Altinn 2 self-identified user (most OIDC providers).
        /// </summary>
        [JsonStringEnumMemberName("legacy")]
        Legacy = 1,

        /// <summary>
        /// Educational self-identified user (UIDP / FEIDE).
        /// </summary>
        [JsonStringEnumMemberName("edu")]
        Educational,

        /// <summary>
        /// ID-porten email self-identified user.
        /// </summary>
        [JsonStringEnumMemberName("idporten-email")]
        IdPortenEmail,
    }
}
