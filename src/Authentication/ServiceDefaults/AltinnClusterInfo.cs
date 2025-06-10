using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Altinn.Platform.Authentication.ServiceDefaults;

/// <summary>
/// Information about the Altinn cluster.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AltinnClusterInfo
{
    /// <summary>
    /// Gets or sets the cluster network.
    /// </summary>
    public IPNetwork? ClusterNetwork { get; set; }
}