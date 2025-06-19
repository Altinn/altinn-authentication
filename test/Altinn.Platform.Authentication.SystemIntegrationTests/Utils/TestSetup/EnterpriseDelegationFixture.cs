using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class EnterpriseDelegationFixture : TestFixture, IAsyncLifetime
{
    public required string SystemId;
    public string? VendorTokenMaskinporten;
    public string? ClientId { get; set; }

    public async Task InitializeAsync()
    {
        VendorTokenMaskinporten = Platform.GetMaskinportenTokenForVendor().Result;
        //Creates System in System Register with these access packages
        string[] accessPackages =
        [
            "urn:altinn:accesspackage:akvakultur"
        ];

        var name = Guid.NewGuid().ToString("N");

        SystemId = await Platform.Common.CreateSystemWithAccessPackages(accessPackages, name);
    }

    public async Task DisposeAsync()
    {
        await Platform.Common.DeleteSystem(SystemId, VendorTokenMaskinporten);
    }
}