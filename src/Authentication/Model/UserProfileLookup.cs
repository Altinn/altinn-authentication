using System;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// Input model for internal UserProfile lookup requests, where one of the lookup identifiers available must be set for performing the lookup request:
///     UserId (from the Altinn user profile)
///     Username (from the Altinn user profile)
///     SSN/Dnr (from Freg)
///     Uuid (from the Party/UserProfile implementation)
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
