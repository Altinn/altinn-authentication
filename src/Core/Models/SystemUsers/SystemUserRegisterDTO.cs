using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    /// <summary>
    /// DTO for syncing the SystemUsers to the Register 
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemUserRegisterDTO
    {
        /// <summary>
        /// GUID Id for the SystemUser
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        /// <summary>
        /// The Title is by default the same as the System's Display Name
        /// </summary>
        [JsonPropertyName("integrationTitle")]
        public required string IntegrationTitle { get; set; }

        /// <summary>
        /// False by default, if a SystemUser is deleted in the API,
        /// it is marked as IsDeleted ("tombstoned") rather than actually deleted.
        /// </summary>
        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Last changed data time
        /// </summary>
        [JsonPropertyName("lastChanged")]
        public DateTime LastChanged {  get; set; }

        /// <summary>
        /// Created date time
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } 

        /// <summary>
        /// Used internally when paginating
        /// </summary>
        [JsonPropertyName("sequenceNo")]
        public long SequenceNo { get; set; } = 0;

        /// <summary>
        /// Either Agent or Default
        /// </summary>
        [JsonPropertyName("systemUserType")]
        public string SystemUserType { get; set; }
    }
}
