namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// This model describes a detail while providing a specific code for identifying a specific recurring detail and parameters needed for context and understanding.
/// Can be extended for specific detailing/metadata/informational purposes.
/// </summary>
public class DetailExternal
{
    /// <summary>
    /// Gets or sets the detail identifier code
    /// </summary>
    public DetailCodeExternal Code { get; set; }

    /// <summary>
    /// Gets or sets a human readable (english) description of the detail
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of parameters which is related to the detail code and description
    /// </summary>
    public Dictionary<string, List<AttributePair>> Parameters { get; set; } = [];
}
