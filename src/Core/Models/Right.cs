namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for a Default Right on a Registered System
    /// </summary>
    public record Right
    {

        /// <summary>
        /// For instance: Read, Write, Sign
        /// </summary>                
        public string? Action { get; set; }

        /// <summary>
        /// The list of attributes that identifes a resource part of a right.
        /// </summary>
        public List<AttributePair> Resource { get; set; } = [];
    }
}