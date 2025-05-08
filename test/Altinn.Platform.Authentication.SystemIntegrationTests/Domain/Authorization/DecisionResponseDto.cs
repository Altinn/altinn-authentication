using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;

public class DecisionResponseDto
{
    [JsonPropertyName("response")]
    public List<DecisionItem>? Response { get; set; }
}

public class DecisionItem
{
    [JsonPropertyName("decision")]
    public string? Decision { get; set; }
}