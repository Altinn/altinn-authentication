namespace Altinn.Platform.Authentication.Integration.AccessManagement;

/// <summary>
/// Feature flags controlling how the <see cref="AccessManagementClient"/> talks to Access Management.
/// The flag names must match the entries under the "FeatureManagement" section in appsettings.
/// </summary>
public static class AccessManagementFeatureFlags
{
    /// <summary>
    /// When enabled, the client-delegation endpoints are called on the Access Management v2 API
    /// (<c>accessmanagement/api/v2/enduser/clientdelegations</c>) instead of v1. In addition to the
    /// version segment, v2 renames the query parameters <c>from</c> to <c>client</c> and <c>to</c> to
    /// <c>agent</c>. Defaults to disabled (v1) when the flag is absent from configuration.
    /// </summary>
    public const string ClientDelegationApiV2 = "ClientDelegationApiV2";
}
