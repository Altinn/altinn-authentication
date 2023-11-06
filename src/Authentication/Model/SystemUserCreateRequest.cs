namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Model for the request object from the Frontend's BFF when creating a new System User (or doing an Update?)
    /// </summary>
    public class SystemUserCreateRequest
    {
        /// <summary>
        /// GUID created by the "real" Authentication Component
        /// When the Frontend send a request for a new SystemUser it may have a temporary Id
        /// Also when doing Updates the Frontend must send in the proper Id
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The Title and Description are strings set by the end-user in the Frontend.
        /// </summary>
        public string IntegrationTitle { get; set; }

        /// <summary>
        /// The user entered Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// For off the shelf systems.
        /// Should probably be human readable (instead of a GUID) but unique string without whitespace
        /// The "real" Authentication Component should validate that the SystemName is unique
        /// Retrieved from the SystemRegister, the full CRUD Api is in a different service
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// The OwnedBy identifies the end-user Organisation, and is fetched from the login Context and
        /// user party serivces
        /// </summary>
        public string OwnedByPartyId { get; set; }

        /// <summary>
        /// For self-made systems, not delivered in the first Phase of the Project, and therefore not in the DTO
        /// In these cases the SupplierName and SupplierOrgNo will be blank
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// The name of the Supplier of the Product used in this Integration.
        /// In later phases, it will be possible to use non-supplier based Products, in which case the ClientId property should be filled out.
        /// </summary>
        public string? SupplierName { get; set; }

        /// <summary>
        /// The organization number for the Supplier of the Product 
        /// In later phases, it will be possible to use non-supplier based Products, in which case the ClientId property should be filled out.
        /// </summary>
        public string? SupplierOrgNo { get; set; }
    }
}
