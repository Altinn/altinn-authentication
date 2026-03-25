using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

[ExcludeFromCodeCoverage]
public class ResourceDtoCheck
{
    /// <summary>
    /// Resource the delegation check is regarding
    /// </summary>
    public required ResourceDto Resource { get; set; }

    /// <summary>
    /// Resource actions
    /// </summary>
    public List<string> Actions { get; set; }

    /// <summary>
    /// Result of the delegation check.
    /// True if the user is authorized to give others access to the resource on behalf of the specified party, false otherwise.
    /// </summary>
    public bool Result { get; set; }

    /// <summary>
    /// Reason for the result of the delegation check.
    /// </summary>
    public IEnumerable<Reason> Reasons { get; set; } = [];
}
