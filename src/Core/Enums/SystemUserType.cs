using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemUserType
    {
        [JsonStringEnumMemberName("standard")]
        Standard,

        [JsonStringEnumMemberName("agent")]
        Agent,

        /// <summary>
        /// A system user for an organisation's own, self-built system: the organisation is both the
        /// vendor and the customer, the underlying Registered System is created together with the
        /// SystemUser in a single operation, and no Rights/AccessPackages are pre-declared — they are
        /// delegated afterwards through the Access Management UI.
        /// </summary>
        [JsonStringEnumMemberName("standalone")]
        Standalone
    }
}
