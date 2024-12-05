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

        public static bool Compare(Right first, Right second)
        {
            if (first.Resource is null && second.Resource is null)
            {
                return true;
            }

            if (first.Resource is null || second.Resource is null)
            {
                return false;
            }

            if (first.Resource?.Count != second.Resource?.Count)
            {
                return false;
            }

            foreach (var resource in first.Resource)
            {
                if (!second.Resource.Any(r => r.Id == resource.Id && r.Value == resource.Value))
                {
                    return false;
                }
            }

            foreach (var resource in second.Resource)
            {
                if (!first.Resource.Any(r => r.Id == resource.Id && r.Value == resource.Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}