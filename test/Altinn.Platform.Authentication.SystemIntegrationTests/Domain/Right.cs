using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class Right
{
    [JsonPropertyName("resource")] public List<Resource> Resource { get; set; } = new();
}

public class Resource
{
    [JsonPropertyName("value")] public string? Value { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }
}