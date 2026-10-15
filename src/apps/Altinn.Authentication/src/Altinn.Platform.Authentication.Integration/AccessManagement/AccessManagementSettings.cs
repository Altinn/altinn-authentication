namespace Altinn.Platform.Authentication.Integration.AccessManagement;

#nullable enable
public class AccessManagementSettings
{
    /// <summary>
    /// Gets or sets the access management api endpoint
    /// </summary>
    public string? ApiAccessManagementEndpoint { get; set; }

    /// <summary>
    /// Name of the cookie for where JWT is stored
    /// </summary>
    public string? JwtCookieName { get; set; }

}
