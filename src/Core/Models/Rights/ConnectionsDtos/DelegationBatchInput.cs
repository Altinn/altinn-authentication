using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

[ExcludeFromCodeCoverage]    
public class DelegationBatchInputDto
{
    [JsonPropertyName("values")]
    public List<AgentPermissionDto> Values { get; set; } = [];

    public class AgentPermissionDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("packages")]
        public List<string> Packages { get; set; }
    }
}
