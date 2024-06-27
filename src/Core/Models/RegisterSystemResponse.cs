using Altinn.Platform.Authentication.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

#nullable enable
namespace Altinn.Platform.Authentication.Core.SystemRegister.Models
{
    /// <summary>
    /// Model for the response of a registered system
    /// A Registered System is a product supplied by a System Vendor,
    /// it may need Rights to use or acccess Resources at a Service Provider.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegisterSystemResponse
    {
        /// <summary>
        /// The primary key to store and edit the Registered System.
        /// Used as a Foreign Key in the System User table.
        /// 
        /// The SystemInternalId never changes throughout the lifecycle 
        /// of the entry.
        /// </summary>
        [Required]
        public Guid SystemInternalId { get; set; } = Guid.Empty;

        /// <summary>
        /// A unique External Id for this System, in human-readable string format.
        /// The id could be in the format of system_vendor_name_plus_name_chosen_by_them.
        /// 
        /// An Optimistic Concurrency pattern to create new System Ids could be used,
        /// where the Id of the System is prefixed with the SystemVendor.
        /// 
        /// When the SystemVendor tries to register a new system (product), 
        /// they should be aware of their own previous system names 
        /// when giving the new system it's id.
        /// 
        /// The Registered Systems are stored using a unique system_internal_id
        /// as the primary key, which allows System Vendors to reuse the SystemId 
        /// if creating new systems to replace the existing one. The old system's
        /// name should be changed prior to be reused, since it must be unique.
        /// </summary>
        [Required]
        public string SystemId { get; set; } = string.Empty;

        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>
        [Required]
        public string SystemVendorOrgNumber { get; set; }

        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>
        [Required]
        public string SystemVendorOrgName { get; set; } = string.Empty;

        /// <summary>
        /// A short name of the product, used when displaying to the user
        /// </summary>
        [Required]
        public string SystemName { get; set; } = string.Empty;

        /// <summary>
        /// The array of Rights versus System Provider's Resources needed to use this Registered System
        /// </summary>
        public List<Right> Rights { get; set; } = [];

        /// <summary>
        /// Registered Systems can be set to Soft Deleted
        /// </summary>
        public bool SoftDeleted { get; set; } = false;

        /// <summary>
        /// The client Id
        /// </summary>
        [Required]
        public List<Guid> ClientId { get; set; } = [];

        /// <summary>
        /// Registered systems can be set to false to hide it from the user interface.
        /// This is used when the vendor does not want the user to create system users for specific systems
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }
}