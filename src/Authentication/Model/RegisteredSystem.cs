using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Model for the System Register
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegisteredSystem
    {
        /// <summary>
        /// The unique Id for this product, in human-readable string format.
        /// The id is in the format of some_words_stiched_together_with_underscore
        /// we use an Optimistic Concurrency pattern to create new System Types Ids,
        /// where the id of the product is prefixed with the SystemVendor to help with uniqueness.
        /// When the SystemVendor tries to register a new system type, they should be aware of their own 
        /// previous system names when giving the new system it's id.
        /// </summary>
        public string SystemTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Which Vendor provides this product
        /// </summary>
        public string SystemVendor { get; set; } = string.Empty;

        /// <summary>
        /// A short description of the product, used when filtering the dropdown menu.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
