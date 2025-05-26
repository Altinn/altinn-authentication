using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.Builders;

public class SystemRegisterBuilder
{
    private readonly SystemRegisterRequestDto _system = new();

    public SystemRegisterBuilder WithId(string id)
    {
        _system.Id = id;
        return this;
    }

    public SystemRegisterBuilder WithVendor(string? vendorId)
    {
        _system.Vendor = new VendorDto { ID = $"0192:{vendorId}" };
        return this;
    }

    public SystemRegisterBuilder WithName(string name)
    {
        _system.Name = new Dictionary<string, string>
        {
            { "en", name },
            { "nb", name },
            { "nn", name }
        };
        return this;
    }

    public SystemRegisterBuilder WithDescription(string description)
    {
        _system.Description = new Dictionary<string, string>
        {
            { "en", description },
            { "nb", description },
            { "nn", description }
        };
        return this;
    }

    public SystemRegisterBuilder WithAccessPackages(IEnumerable<string> accessPackageUrns)
    {
        _system.AccessPackages = accessPackageUrns.Select(urn => new AccessPackageDto { Urn = urn }).ToList();
        return this;
    }

    public SystemRegisterBuilder WithClientId(string clientId)
    {
        _system.ClientId = new List<string> { clientId };
        return this;
    }

    public SystemRegisterBuilder WithRedirectUrl(string url)
    {
        _system.AllowedRedirectUrls = new List<string> { url };
        return this;
    }

    public SystemRegisterBuilder IsVisible(bool isVisible)
    {
        _system.IsVisible = isVisible;
        return this;
    }

    public SystemRegisterRequestDto Build() => _system;
}