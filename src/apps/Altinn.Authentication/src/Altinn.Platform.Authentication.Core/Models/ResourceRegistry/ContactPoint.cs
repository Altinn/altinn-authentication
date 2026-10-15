namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// Defines a contact point.
    /// </summary>
    /// <remarks>
    /// Nothing in this service reads a contact point, and no observed resource payload has
    /// bound one, so every member is nullable.
    /// </remarks>
    public class ContactPoint
    {
        /// <summary>
        /// The type of contact point, phone, email ++
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// The contact details. The actual phone number, email adress
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Phone details
        /// </summary>
        public string? Telephone { get; set; }

        /// <summary>
        /// Contact page
        /// </summary>
        public string? ContactPage { get; set; }
    }
}
