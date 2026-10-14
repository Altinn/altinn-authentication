using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Reason for access (Dto)
/// </summary>
[ExcludeFromCodeCoverage]
public class AssignmentDto
{
    /// <summary>
    /// Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// RoleId
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// FromId
    /// </summary>
    public Guid FromId { get; set; }

    /// <summary>
    /// ToId
    /// </summary>
    public Guid ToId { get; set; }
}
