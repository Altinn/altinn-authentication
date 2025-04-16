namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;

public class SystemRegisterRequestDto
{
    public string Id { get; set; }
    public VendorDto Vendor { get; set; }
    public Dictionary<string, string> Name { get; set; }
    public Dictionary<string, string> Description { get; set; }
    public List<AccessPackageDto> AccessPackages { get; set; }
    public List<string> AllowedRedirectUrls { get; set; }
    public bool IsVisible { get; set; }
    public List<string> ClientId { get; set; }
}

public class VendorDto
{
    public string ID { get; set; }
}

public class AccessPackageDto
{
    public string Urn { get; set; }
}