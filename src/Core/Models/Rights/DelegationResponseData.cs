namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
///  Represents the response of the backend for Delegation access check response
/// </summary>
public class DelegationResponseData
{
    /// <summary>
    /// The key for the right.
    /// </summary>
    public string? RightKey { get; set; }

    /// <summary>
    /// The list of id value that together defines the resource.
    /// </summary>
    public List<AttributePair> Resource { get; set; } = [];

    /// <summary>
    /// The action performed.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The status of the response.
    /// </summary>
    public string? Status { get; set; }
}