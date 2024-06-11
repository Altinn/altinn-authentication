namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for a Default Right on a Registered System
    /// </summary>
    public class Right
    {
        /// <summary>
        /// The list of resources at the Service Provider which the Right is for.
        /// </summary>
        public List<AttributePair> Resources { get; set; } = [];
    }
}