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
    public class SystemUserUpdateDto
    {
        /// <summary>
        /// GUID created by the "real" Authentication Component, identifying the SystemUser to update.
        /// Required: UpdateSystemUserById parses it without a null check, so a body without an id
        /// is rejected during model binding rather than failing later.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        /// <summary>
        /// The PartyId identifies the end-user Organisation, and is fetched from the login Context and
        /// user party serivces
        /// Even if this DTO allows null, the db field is of course still required
        /// </summary>
        [JsonPropertyName("partyId")]
        public string? PartyId { get; set; }

        /// <summary>
        /// The Organisation Number for the end-user as it is stored in ER Registry        
        /// </summary>
        [JsonPropertyName("reporteeOrgNo")]
        public string? ReporteeOrgNo { get; set; }

        /// <summary>
        /// The Title is set by the end-user in the Frontend, by default it is the same as the System's Display Name
        /// Even if this DTO allows null, the db field is of course still required     
        /// </summary>
        [JsonPropertyName("integrationTitle")]
        public string? IntegrationTitle { get; set; }

        /// <summary>
        /// For off the shelf systems.
        /// Should probably be human readable (instead of a GUID) but unique string without whitespace
        /// The "real" Authentication Component should validate that the SystemName is unique
        /// Retrieved from the SystemRegister, the full CRUD Api is in a different service
        /// Even if this DTO allows null, the db field is of course still required
        /// </summary>
        [JsonPropertyName("systemId")]
        public string? SystemId { get; set; }
    }
}
