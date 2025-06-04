using System.Net;

namespace Altinn.Platform.Authentication.ServiceDefaults;

/// <summary>
/// Information about the Altinn cluster.
/// </summary>
public sealed class AltinnClusterInfo
{
    /// <summary>
    /// Gets or sets the cluster network.
    /// </summary>
    public IPNetwork? ClusterNetwork { get; set; }
}