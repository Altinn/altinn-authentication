using Altinn.Platform.Authentication.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

#nullable enable
namespace Altinn.Platform.Authentication.Core.SystemRegister.Models
{
    /// <summary>
    /// Model for the System Register
    /// A Registered System is a product supplied by a System Vendor,
    /// it may need Rights to use or acccess Resources at a Service Provider.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegisterSystemRequest
    {
        /// <summary>
        /// The unique Id for this product, in human-readable string format.
        /// The id is in the format of vendororgnumber_plus_name_chosen_by_them.
        /// 
        /// An Optimistic Concurrency pattern to create new System Ids is used,
        /// where the Id of the product is prefixed with the SystemVendor to help with uniqueness.
        /// 
        /// When the SystemVendor tries to register a new system (product), 
        /// they should be aware of their own previous system names 
        /// when giving the new system it's id.
        /// </summary>
     
        public required string Id { get; init; }

        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>
        public required VendorInfo Vendor { get; set; }

        /// <summary>
        /// A short name of the product, used when displaying to the user
        /// </summary>
        public required IDictionary<string, string> Name { get; set; }

        /// <summary>
        /// A short description of the product, used when displaying to the user
        /// </summary>
        public required IDictionary<string, string> Description { get; set; }

        /// <summary>
        /// The array of Rights versus System Provider's Resources needed to use this Registered System
        /// </summary>
        public List<Right> SingleRights { get; set; } = [];

        /// <summary>
        /// Registered Systems can be set to Soft Deleted
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// The client Id
        /// </summary>
        public required List<String> ClientId { get; set; }

        /// <summary>
        /// Registered systems can be set to false to hide it from the user interface.
        /// This is used when the vendor does not want the user to create system users for specific systems
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// White listing of redirect urls
        /// </summary>
        public List<Uri> AllowedRedirectUrls { get; set; } = [];
    }
}