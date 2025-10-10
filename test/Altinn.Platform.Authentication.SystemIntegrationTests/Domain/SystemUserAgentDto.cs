namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

public class SystemUserAgentDto
{
    public Guid Id { get; set; }
    public string IntegrationTitle { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SystemInternalId { get; set; } = string.Empty;
    public string PartyId { get; set; } = string.Empty;
    public string PartyUuId { get; set; } = string.Empty;
    public string ReporteeOrgNo { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public bool IsDeleted { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierOrgno { get; set; } = string.Empty;
    public string ExternalRef { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public List<AccessPackage> AccessPackages { get; set; } = [];
}

public class AccessPackage
{
    public string Urn { get; set; }
}