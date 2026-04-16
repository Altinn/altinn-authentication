using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;

[ExcludeFromCodeCoverage]    
public class DelegationBatchInputDto
{
    [JsonPropertyName("values")]
    public List<RoleAccessPackagesPrimitive> Values { get; set; } = [];
}
