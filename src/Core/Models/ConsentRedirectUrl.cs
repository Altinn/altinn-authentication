namespace Altinn.Platform.Authentication.Core.Models;

/// <summary>
/// Represents a redirect URL for consent requests.
/// </summary>
public class ConsentRedirectUrl
{
    /// <summary>
    /// Gets or sets the URL to redirect user to after consent is granted or denied.
    /// </summary>
    public string? Url { get; set; }
}
