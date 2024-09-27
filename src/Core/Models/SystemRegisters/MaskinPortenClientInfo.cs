namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for the maskinporten client information for the system
    /// </summary>
    public class MaskinPortenClientInfo
    {

        /// <summary>
        /// Maskinporten client id for the system
        /// </summary>                
        public required string ClientId { get; set; }

        /// <summary>
        /// internal id for the system
        /// </summary>                
        public required Guid SystemInternalId { get; set; }

        /// <summary>
        /// true if the clientid is not in use
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}