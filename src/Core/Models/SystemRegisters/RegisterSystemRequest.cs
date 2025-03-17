using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

#nullable enable
namespace Altinn.Platform.Authentication.Core.SystemRegister.Models
{
    /// <summary>
    /// Model for the response of a registered system
    /// A Registered System is a product supplied by a System Vendor,
    /// it may need Rights to use or acccess Resources at a Service Provider.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegisterSystemRequest
    {
        /// <summary>
        /// The System Id is a unique External Id for this System, in human-readable string format. (SLUG)
        /// The id could be in the format of system_vendor_orgno_plus_name_chosen_by_them.
        /// 
        /// An Optimistic Concurrency pattern to create new System Ids could be used,
        /// where the Id of the System is prefixed with the SystemVendor's orgno.
        /// 
        /// When the SystemVendor tries to register a new system (product), 
        /// they should be aware of their own previous system names 
        /// when giving the new system it's id.
        /// 
        /// The Registered Systems are stored using a unique internal_id
        /// as the primary key, which allows System Vendors to reuse the external System's Id 
        /// if creating new systems to replace the existing one. The old system's
        /// name should be changed prior to be reused, since it must be unique.
        /// </summary>        
        public required string Id { get; set; } 

        /// <summary>
        /// The primary key to store and edit the Registered System.
        /// Used as a Foreign Key in the System User table.
        /// 
        /// Created by our system, and is not visible to the System Vendor.
        /// 
        /// The InternalId never changes throughout the lifecycle 
        /// of the entry.
        /// </summary>
        [JsonIgnore]
        public Guid InternalId { get; set; } = Guid.Empty;

        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>
        public required VendorInfo Vendor { get; set; }


        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>
        [JsonIgnore]
        public string SystemVendorOrgNumber => Vendor.ID;

        /// <summary>
        /// Organization number of the system Vendor that offers the product (system)
        /// </summary>        
        [JsonIgnore]
        public string SystemVendorOrgName { get; } = string.Empty;

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
        public List<Right>? Rights { get; set; } = [];

        /// <summary>
        /// The array of access package(s) that is required to use this Registered System
        /// </summary>
        public List<AccessPackage>? AccessPackages { get; set; } = [];

        /// <summary>
        /// The client Id
        /// </summary>
        public List<string> ClientId { get; set; } = [];

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