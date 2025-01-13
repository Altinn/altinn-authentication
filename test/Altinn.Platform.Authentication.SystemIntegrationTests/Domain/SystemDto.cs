namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

// DTO Classes
public class SystemDto
{
    public string SystemId { get; set; }
    public string SystemVendorOrgNumber { get; set; }
    public string SystemVendorOrgName { get; set; }
    public bool isVisible { get; set; }
    public bool isDeleted { get; set; }
}

public class SystemsDto
{
    public List<SystemDto> Systems { get; set; }
}