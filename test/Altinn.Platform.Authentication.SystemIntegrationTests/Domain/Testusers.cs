namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

using System.Text.Json.Serialization;

public class Testuser
{
    [JsonPropertyName("altinnPartyId")] public string? AltinnPartyId { get; set; }

    [JsonPropertyName("pid")] public string? Pid { get; set; }

    [JsonPropertyName("userId")] public string? UserId { get; set; }

    [JsonPropertyName("role")] public string? Role { get; set; }

    [JsonPropertyName("org")] public string? Org { get; set; }
    [JsonPropertyName("altinnPartyUuid")] public string? AltinnPartyUuid { get; set; }
}