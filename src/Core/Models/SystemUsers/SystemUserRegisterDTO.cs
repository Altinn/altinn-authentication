using Altinn.Platform.Authentication.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// DTO for syncing the SystemUsers to the Register 
/// DTO for synkronisering av SystemUsers til Registeret
/// </summary>
[ExcludeFromCodeCoverage]
public class SystemUserRegisterDTO
{
    /// <summary>
    /// GUID Id for the SystemUser
    /// Norsk: GUID Id for SystemUser
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The organisation number for the SystemUser's Party ( the customer that delegates rights to the systemuser).
    /// Is one of the three parts of the External Request Id.
    /// Norsk: Organisasjonsnummeret til SystemUser sin Part (kunden som delegerer rettigheter til systembrukeren).
    /// Norsk: Er en av de tre delene av den eksterne forespørsels-IDen.
    /// </summary>
    [JsonPropertyName("partyOrgNo")]
    public required string PartyOrgNo { get; set; }

    /// <summary>
    /// The Reportee Id, used internally, old integer format, exchanged in the DTO as string
    /// Norsk: Reportee Id, brukt internt, gammelt heltallsformat, utvekslet i DTO som streng.
    /// </summary>
    [JsonPropertyName("partyId")]
    public required string PartyId { get; set; }

    /// <summary>
    /// The Title is by default the same as the System's Display Name
    /// Norsk: Tittelen er som standard den samme som systemets visningsnavn.
    /// </summary>
    [JsonPropertyName("integrationTitle")]
    public required string IntegrationTitle { get; set; }

    /// <summary>
    /// False by default, if a SystemUser is deleted in the API,
    /// it is marked as IsDeleted ("tombstoned") rather than actually deleted.
    /// Norsk: Standard er 'false', hvis en SystemUser slettes i API-et, blir den merket som IsDeleted ("tombstoned") i stedet for å bli slettet.
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Last changed data time
    /// Norsk: Siste endringstidspunkt
    /// </summary>
    [JsonPropertyName("lastChanged")]
    public DateTime LastChanged {  get; set; }

    /// <summary>
    /// Created date time
    /// Norsk: Opprettelsestidspunkt
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } 

    /// <summary>
    /// Used internally when paginating
    /// Norsk: Brukes internt ved paginering
    /// </summary>
    [JsonPropertyName("sequenceNo")]
    public long SequenceNo { get; set; } = 0;

    /// <summary>
    /// Either Agent or Default
    /// Norsk: Enten Agent eller Default
    /// </summary>
    [JsonPropertyName("systemUserType")]
    public SystemUserType SystemUserType { get; set; }
}
