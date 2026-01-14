using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.SystemRegisters
{
    /// <summary>
    /// Enum representing the type of change for system change log.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemChangeType
    {
        [JsonStringEnumMemberName("create")]
        Create,

        [JsonStringEnumMemberName("update")]
        Update,

        [JsonStringEnumMemberName("rightsupdate")]
        RightsUpdate,

        [JsonStringEnumMemberName("accesspackageupdate")]
        AccessPackageUpdate,
        
        [JsonStringEnumMemberName("delete")] 
        Delete,

        [JsonStringEnumMemberName("unknown")]
        Unknown,
    }
}
