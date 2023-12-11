using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// The model of the System User response given in the CRUD API in SystemUserController.cs
    /// This model will be exchanged between this Authentication component, the PostGress db and the BFF for the Frontend.
    /// The BFF will provide a tailored DTO to the Frontend.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemUserUpdateDTO
    {
        /// <summary>
        /// GUID created by the "real" Authentication Component
        /// When the Frontend send a request for the creation of a new SystemUser the Id is null
        /// </summary>
        [AllowNull]
        [JsonPropertyName("Id")]
        public string Id { get; set; }

        /// <summary>
        /// The OwnedBy identifies the end-user Organisation, and is fetched from the login Context and
        /// user party serivces
        /// </summary>
        [Required]
        [JsonPropertyName("OwnedByPartyId")]
        public string OwnedByPartyId { get; set; }

        /// <summary>
        /// The Title and Description are strings set by the end-user in the Frontend.
        /// </summary>
        [JsonPropertyName("IntegrationTitle")]
        public string IntegrationTitle { get; set; }

        /// <summary>
        /// For off the shelf systems.
        /// Should probably be human readable (instead of a GUID) but unique string without whitespace
        /// The "real" Authentication Component should validate that the SystemName is unique
        /// Retrieved from the SystemRegister, the full CRUD Api is in a different service
        /// </summary>
        [JsonPropertyName("ProductName")]
        public string ProductName { get; set; }
    }
}
