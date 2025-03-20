using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemUserType
    {
        [JsonStringEnumMemberName("standard")]
        Standard,

        [JsonStringEnumMemberName("agent")]
        Agent
    }
}
