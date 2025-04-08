using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// A DTO for SystemUser without internal fields,
/// in use for instance on the Maskinporten Endpoint
/// </summary>
public class SystemUserDTO
{
    /// <summary>
    /// The GUID Id is the primary key for the SystemUser in the Authentication db.
    /// It also occurs as the PartyUuid when the SystemUser is an Entity in the Access Management APIs
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Identifier for off the shelf systems, registered in the SystemRegister db.
    /// Should be human readable (instead of a GUID) and unique string without whitespace.                
    /// </summary>
    [JsonPropertyName("systemId")]
    public required string SystemId { get; set; }

    /// <summary>
    /// The PartyUuid identifies the end-user/Customer Organisation, and is fetched from the login Context and
    /// user party serivces. 
    /// 
    /// Is of the form GUID PartyUuid
    /// 
    /// The PartyUuid is an id created by Altin.Authentication/AccessManagement.
    /// It is used as an umbrella over SSN, OrgNo, SystemUserId and similar external Ids.
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public required string PartyUuid { get; set; }

    /// <summary>
    /// The Organisation Number for the end-user/Customer Organisation as it is stored in the ER Registry            
    /// </summary>
    [JsonPropertyName("reporteeOrgNo")]
    public required string ReporteeOrgNo { get; set; } 

    /// <summary>
    /// The organization number for the Supplier of the Product     
    /// (Also called Vendor in some contexts.)
    /// </summary>
    [JsonPropertyName("supplierOrgno")]
    public required string SupplierOrgNo { get; set; }

    /// <summary>
    /// The External Reference is provided by the Vendor, in the Request to create a new SystemUser, 
    /// it is used as a disambiguation field; if their Customers have several sub-units,
    /// since normally a SystemUser can only have a single occurance of a ReporteeOrgNo pr SystemId.
    /// </summary>
    [JsonPropertyName("externalRef")]
    public required string ExternalRef { get; set; } 
}
