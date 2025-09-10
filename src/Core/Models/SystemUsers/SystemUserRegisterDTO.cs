using Altinn.Platform.Authentication.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// DTO for syncing the SystemUsers to the Register 
/// </summary>
[ExcludeFromCodeCoverage]
public class SystemUserRegisterDTO
{
    /// <summary>
    /// GUID Id for the SystemUser
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The organisation number for the SystemUser's Party ( the customer that delegates rights to the systemuser).
    /// Is one of the three parts of the External Request Id.
    /// </summary>
    [JsonPropertyName("partyOrgNo")]
    public required string PartyOrgNo { get; set; }

    /// <summary>
    /// The Reportee Id, used internally, old integer format, exchanged in the DTO as string
    /// </summary>
    [JsonPropertyName("partyId")]
    public required string PartyId { get; set; }

    /// <summary>
    /// The Title is by default the same as the System's Display Name
    /// </summary>
    [JsonPropertyName("integrationTitle")]
    public required string IntegrationTitle { get; set; }

    /// <summary>
    /// False by default, if a SystemUser is deleted in the API,
    /// it is marked as IsDeleted ("tombstoned") rather than actually deleted.
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Last changed data time
    /// </summary>
    [JsonPropertyName("lastChanged")]
    public DateTime LastChanged {  get; set; }

    /// <summary>
    /// Created date time
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } 

    /// <summary>
    /// Used internally when paginating
    /// </summary>
    [JsonPropertyName("sequenceNo")]
    public long SequenceNo { get; set; } = 0;

    /// <summary>
    /// Either Agent or Default
    /// </summary>
    [JsonPropertyName("systemUserType")]
    public SystemUserType SystemUserType { get; set; }
}
