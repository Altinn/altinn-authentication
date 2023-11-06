namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// The model of the System User response given in the CRUD API in SystemUserController.cs
    /// This model will be exchanged between this Authentication component, the PostGress db and the BFF for the Frontend.
    /// The BFF will provide a tailored DTO to the Frontend.
    /// </summary>
    public class SystemUserResponse
    {
        /// <summary>
        /// GUID created by the "real" Authentication Component
        /// When the Frontend send a request for the 
        /// </summary>
        public string Id { get; set; }

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
        /// Nice to have for debugging and logging.
        /// </summary>
        public string Created { get; set; }

        /// <summary>
        /// False by default, if a SystemUser is deleted in the API,
        /// it is marked as IsDeleted ("tombstoned") rather than actually deleted
        /// from the database. This is to avoid complications with cascade delete,
        /// and the need to maintain consistent logging, and possible compliance rules.
        /// </summary>
        public bool IsDeleted { get; set; }

#nullable enable
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

        /// <summary>
        /// For self-made systems, not delivered in the first Phase of the Project, and therefore not in the DTO
        /// In these cases the SupplierName and SupplierOrgNo will be blank
        /// </summary>
        public string? ClientId { get; set; }
    }
}
