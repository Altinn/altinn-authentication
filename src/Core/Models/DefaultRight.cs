namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for a Default Right on a Registered System
    /// </summary>
    public class DefaultRight
    {
        /// <summary>
        /// The identifier for the Right
        /// </summary>
        public string Right { get; set; } = string.Empty;

        /// <summary>
        /// The resource at the Service Provider which the Right is for.
        /// </summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// The identifier for the Service Provider of the Resource.        
        /// </summary>
        public string ServiceProvider { get; set; } = string.Empty;
    }
}
