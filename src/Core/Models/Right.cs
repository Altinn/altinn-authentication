namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// DTO for a Default Right on a Registered System
    /// </summary>
    public class Right
    {
        /// <summary>
        /// The list of attributes that identifes a resource part of a right.
        /// </summary>
        public List<AttributePair> Resource { get; set; } = [];
    }
}