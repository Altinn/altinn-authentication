using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

public class SystemRegisterState
{
    public string? Token { get; set; }
    public string? VendorId { get; set; }
    public string? Name { get; set; }
    public string? ClientId { get; set; }
    public string SystemId => $"{VendorId}_{Name}"; // Combination of vendorId and randomNames
    public string RedirectUrl { get; set; }

    public List<Right> Rights { get; set; } = [];

    public SystemRegisterState()
    {
        Name = Guid.NewGuid().ToString();
    }

    public SystemRegisterState WithVendor(string vendorId)
    {
        VendorId = vendorId;
        return this;
    }

    public SystemRegisterState WithClientId(string clientId)
    {
        ClientId = clientId;
        return this;
    }

    public SystemRegisterState WithToken(string? token)
    {
        Token = token;
        return this;
    }

    public SystemRegisterState WithRedirectUrl(string redirectUrl)
    {
        RedirectUrl = redirectUrl;
        return this;
    }
    
    public SystemRegisterState WithResource(string value, string id)
    {
        var resource = new Resource { Value = value, Id = id };

        if (Rights.Count == 0)
        {
            // Initialize Rights with a new Right that has an initialized Resource list
            Rights.Add(new Right { Resource = new List<Resource> { resource } });
        }
        else
        {
            // Add to the existing Resource list in the first Right
            Rights.First().Resource?.Add(resource);
        }

        return this;
    }
}