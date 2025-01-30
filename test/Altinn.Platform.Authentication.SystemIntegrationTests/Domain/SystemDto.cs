namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

// DTO Classes
public class SystemDto
{
    public string Id { get; set; }
    public string IntegrationTitle { get; set; }
    public string SystemId { get; set; }
    public string ProductName { get; set; }
    public string SystemInternalId { get; set; }
    public string PartyId { get; set; }
    public string ReporteeOrgNo { get; set; }
    public DateTime Created { get; set; }
    public bool IsDeleted { get; set; }
    public string SupplierName { get; set; }
    public string SupplierOrgno { get; set; }
    public string ExternalRef { get; set; }
}

public class SystemsDto
{
    public List<SystemDto> Systems { get; set; }
}