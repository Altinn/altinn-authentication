namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for a Default Right on a Registered System
    /// </summary>
    public class DefaultRight
    {
        /// <summary>
        /// For instance: Read, Write, Sign
        /// Optional
        /// </summary>                
        public string? ActionRight { get; set; }

        /// <summary>
        /// The list of resources at the Service Provider which the Right is for.
        /// </summary>
        public List<AttributePair> Resources { get; set; } = [];

        /// <summary>
        /// The identifier for the Service Provider of the Resource.        
        /// </summary>
        public string ServiceProvider { get; set; } = string.Empty;
    }
}