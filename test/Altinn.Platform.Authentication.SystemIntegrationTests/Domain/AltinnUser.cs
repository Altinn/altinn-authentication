namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

/// <summary>
/// Domain model for fetching Altinn token
/// </summary>
public class AltinnUser
{
    /// <summary>
    /// Party = Organization (org-id)
    /// </summary>
    public string? party; //"50692553" 

    /// <summary>
    /// Unik verdi. Brukes kun av personer. 
    /// </summary>
    public string? userId; //"20012772";

    /// <summary>
    /// Partyid
    /// </summary> (intern altinnId)
    public string? altinnPartyId; // "50822874"; (From console in GUI - "AltinnPartyId")

    /// <summary>
    /// Scopes. On the format: //"altinn:authentication/systemuser.request.read";
    /// </summary>
    public string? scopes;

    /// <summary>
    /// Personal identification number
    /// </summary>
    public string? pid; //"04855195742";
    
}