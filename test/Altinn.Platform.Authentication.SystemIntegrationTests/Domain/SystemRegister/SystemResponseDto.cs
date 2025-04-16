namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class SystemResponseDto
{
    public required string SystemId { get; set; }
    public required string SystemVendorOrgNumber { get; set; }
    public required string SystemVendorOrgName { get; set; }
    public required bool isVisible { get; set; }
    public bool isDeleted { get; set; }
}
