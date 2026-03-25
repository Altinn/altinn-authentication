using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Extended Resource
/// </summary>
[ExcludeFromCodeCoverage]
public class ResourceDto
{
    /// <summary>
    /// Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ProviderId
    /// </summary>
    public Guid ProviderId { get; set; }

    /// <summary>
    /// TypeId
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Reference identifier
    /// </summary>
    public string RefId { get; set; }

    /// <summary>
    /// Provider
    /// </summary>
    public ProviderDto Provider { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public TypeDto Type { get; set; }
}
