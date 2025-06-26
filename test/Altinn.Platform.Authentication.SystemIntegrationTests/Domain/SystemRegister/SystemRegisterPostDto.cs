namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;

public class SystemRegisterRequestDto
{
    public string? Id { get; set; }
    public VendorDto? Vendor { get; set; }
    public Dictionary<string, string>? Name { get; set; }
    public Dictionary<string, string>? Description { get; set; }
    public List<SystemRegisterAccessPackageDto>? AccessPackages { get; set; }
    public List<string>? AllowedRedirectUrls { get; set; }
    public bool IsVisible { get; set; }
    public List<string>? ClientId { get; set; }
}

public class VendorDto
{
    public string? ID { get; set; }
}

public class SystemRegisterAccessPackageDto
{
    public string? Urn { get; set; }
    
    public override bool Equals(object? obj)
    {
        return obj is SystemRegisterAccessPackageDto other && Urn == other.Urn;
    }
}