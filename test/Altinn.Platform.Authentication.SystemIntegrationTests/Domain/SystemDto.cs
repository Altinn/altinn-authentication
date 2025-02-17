using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class SystemDto
{
    public required string SystemId { get; set; }
    public required string SystemVendorOrgNumber { get; set; }
    public required string SystemVendorOrgName { get; set; }
    public required bool isVisible { get; set; }
    public bool isDeleted { get; set; }
}

public class SystemsDto
{
    public required List<SystemDto> Systems { get; set; }
}