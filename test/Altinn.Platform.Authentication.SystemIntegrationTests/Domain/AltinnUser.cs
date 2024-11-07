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
    /// </summary>
    public string? partyId; // "50822874";

    /// <summary>
    /// Scopes. On the format: //"altinn:authentication/systemuser.request.read";
    /// </summary>
    public string? scopes;

    /// <summary>
    /// Personal identification number
    /// </summary>
    public string? pid; //"04855195742";
    
}