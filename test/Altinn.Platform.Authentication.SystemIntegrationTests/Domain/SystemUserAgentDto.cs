namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class SystemUserAgentDto
{
    public Guid Id { get; set; }
    public string IntegrationTitle { get; set; }
    public string SystemId { get; set; }
    public string ProductName { get; set; }
    public string SystemInternalId { get; set; }
    public string PartyId { get; set; }
    public string PartyUuId { get; set; }
    public string ReporteeOrgNo { get; set; }
    public DateTime Created { get; set; }
    public bool IsDeleted { get; set; }
    public string SupplierName { get; set; }
    public string SupplierOrgno { get; set; }
    public string ExternalRef { get; set; }
    public string UserType { get; set; }
    public List<AccessPackage> AccessPackages { get; set; } = new();
}

public class AccessPackage
{
    public string Urn { get; set; }
}