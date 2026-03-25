using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

[ExcludeFromCodeCoverage]
public class RightDto
{
    /// <summary>
    /// Unique key for action
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Name of the action to present in frontend
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Concatenated key for subresources from policy rule
    /// </summary>
    public IEnumerable<AttributeDto> Resource { get; set; }

    /// <summary>
    /// Action
    /// </summary>
    public AttributeDto Action { get; set; }
}
