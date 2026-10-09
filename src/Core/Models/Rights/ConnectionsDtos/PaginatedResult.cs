using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

public class PaginatedResult<T> where T : class
{
    [JsonPropertyName("links")]
    public Link Links { get; set; } = new();

    [JsonPropertyName("data")]
    public T? Data { get; set; } = null;
}

public class Link
{
    [JsonPropertyName("next")]
    public string? Next { get; set; } = null;
}