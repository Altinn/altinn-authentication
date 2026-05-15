using System.Text.Json.Serialization;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Wire payload for <c>POST /register/api/v2/internal/users/self-identified</c>.
    /// </summary>
    public sealed class SelfIdentifiedUserProvisioningRequest
    {
        /// <summary>
        /// Gets or sets the self-identified user type. Drives the response <c>externalUrn</c> shape.
        /// </summary>
        [JsonPropertyName("selfIdentifiedUserType")]
        public SelfIdentifiedUserType SelfIdentifiedUserType { get; set; }

        /// <summary>
        /// Gets or sets the bridge-shape external identity (e.g. <c>iss:sub</c> for OIDC,
        /// <c>urn:altinn:person:idporten-email:&lt;base64-email&gt;</c> for idporten-email).
        /// The caller (this service) builds it; register does not transform it.
        /// </summary>
        [JsonPropertyName("externalIdentity")]
        public string ExternalIdentity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username to assign on create. Required. Caller-owned; register passes it through.
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email. Required when <see cref="SelfIdentifiedUserType"/>
        /// is <see cref="SelfIdentifiedUserType.IdPortenEmail"/>; ignored otherwise.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
