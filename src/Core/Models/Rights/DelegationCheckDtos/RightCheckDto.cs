namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Right check Dto for response of delegation check
/// </summary>
public class RightCheckDto
{
    /// <summary>
    /// Right key data
    /// </summary>
    public required RightDto Right { get; set; }

    /// <summary>
    /// Result of the delegation check.
    /// True if the user is authorized to give others access to the resource on behalf of the specified party, false otherwise.
    /// </summary>
    public required bool Result { get; set; }

    /// <summary>
    /// Reason for the result of the delegation check.
    /// </summary>
    internal IEnumerable<Permision> Permissions { get; set; } = [];

    /// <summary>
    /// List of reasons for permit or deny
    /// </summary>
    public IEnumerable<DelegationCheckReasonCode> ReasonCodes { get; set; }
}
