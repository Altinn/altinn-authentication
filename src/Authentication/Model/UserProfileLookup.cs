using System;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// Input model for internal UserProfile lookup requests, where one of the lookup identifiers available must be set for performing the lookup request:
///     UserId (from Altinn 2 Authn UserProfile)
///     Username (from Altinn 2 Authn UserProfile)
///     SSN/Dnr (from Freg)
///     Uuid (from Altinn 2 Party/UserProfile implementation will be added later)
/// </summary>
public class UserProfileLookup
{
    /// <summary>
    /// Gets or sets the users UserId if the lookup is to be performed based on this identifier
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the users UserUuid if the lookup is to be performed based on this identifier
    /// </summary>
    public Guid? UserUuid { get; set; }

    /// <summary>
    /// Gets or sets the users Username if the lookup is to be performed based on this identifier
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the users social security number or d-number from Folkeregisteret if the lookup is to be performed based on this identifier
    /// </summary>
    public string Ssn { get; set; }
}
