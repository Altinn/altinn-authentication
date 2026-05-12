using System.Text.Json.Serialization;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Wire payload for the 200 OK response from
    /// <c>POST /register/api/v2/internal/users/self-identified</c>.
    /// </summary>
    public sealed class SelfIdentifiedUserProvisioningResponse
    {
        /// <summary>Gets or sets the party UUID assigned to the user.</summary>
        [JsonPropertyName("partyUuid")]
        public Guid PartyUuid { get; set; }

        /// <summary>Gets or sets the legacy numeric party id.</summary>
        [JsonPropertyName("partyId")]
        public uint PartyId { get; set; }

        /// <summary>Gets or sets the legacy numeric user id.</summary>
        [JsonPropertyName("userId")]
        public uint UserId { get; set; }

        /// <summary>Gets or sets the username.</summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Gets or sets the self-identified user type.</summary>
        [JsonPropertyName("selfIdentifiedUserType")]
        public SelfIdentifiedUserType SelfIdentifiedUserType { get; set; }

        /// <summary>
        /// Gets or sets the canonical external URN representing the user.
        /// <see langword="null"/> for <see cref="SelfIdentifiedUserType.Educational"/>.
        /// </summary>
        [JsonPropertyName("externalUrn")]
        public string? ExternalUrn { get; set; }
    }
}
