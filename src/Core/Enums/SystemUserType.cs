using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemUserType
    {
        [JsonStringEnumMemberName("default")]
        Default,

        [JsonStringEnumMemberName("agent")]
        Agent
    }
}
