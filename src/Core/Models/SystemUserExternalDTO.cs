using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models
{
#nullable enable
    /// <summary>
    /// This model will be exposed to the external api consumers.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemUserExternalDTO
    {
        /// <summary>
        /// GUID created by the "real" Authentication Component
        /// When the Frontend send a request for the creation of a new SystemUser the Id is null
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The Title is by default the same as the System's Display Name
        /// </summary>
        [JsonPropertyName("integrationTitle")]
        public string IntegrationTitle { get; set; } = string.Empty;

        /// <summary>
        /// Identifier for off the shelf systems, registered in the SystemRegister db.
        /// Should be human readable (instead of a GUID) and unique string without whitespace.                
        /// </summary>
        [JsonPropertyName("systemId")]
        public string SystemId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier for off the shelf systems, registered in the SystemRegister db.
        /// Should be human readable (instead of a GUID) and unique string without whitespace.                
        /// To be deprecated, use the systemId field going forward, it contains the same value.
        /// </summary>
        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// The Organisation Number of the owner of the system user       
        /// </summary>
        [JsonPropertyName("reporteeOrgNo")]
        public string ReporteeOrgNo { get; set; } = string.Empty;

        /// <summary>
        /// The date and time the SystemUser was created
        /// </summary>
        [JsonPropertyName("created")]
        public System.DateTime Created { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// False by default, if a SystemUser is deleted in the API,
        /// it is marked as IsDeleted ("tombstoned") rather than actually deleted
        /// from the database. This is to avoid complications with cascade delete,
        /// and the need to maintain consistent logging, and possible compliance rules.
        /// </summary>
        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// The name of the Supplier of the Product used in this Integration.
        /// In later phases, it will be possible to use non-supplier based Products, in which case the ClientId property should be filled out.
        /// </summary>
        [JsonPropertyName("supplierName")]
        public string SupplierName { get; set; } = string.Empty;

        /// <summary>
        /// The organization number for the Supplier of the Product 
        /// In later phases, it will be possible to use non-supplier based Products, in which case the ClientId property should be filled out.
        /// </summary>
        [JsonPropertyName("supplierOrgno")]
        public string SupplierOrgNo { get; set; } = string.Empty;

        /// <summary>
        /// The External Reference is provided by the Vendor, and is used to identify their Customer in the Vendor's system.
        /// </summary>
        [JsonPropertyName("externalRef")]
        public string ExternalRef { get; set; } = string.Empty;

        /// <summary>
        /// The system user type
        /// </summary>
        [JsonPropertyName("userType")]
        public SystemUserType UserType { get; set; }
    }
}
