using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class EnterpriseDelegationFixture : TestFixture, IAsyncLifetime
{
    public required string SystemId;
    public string? VendorTokenMaskinporten;
    public string? ClientId { get; set; }

    public async Task InitializeAsync()
    {
        //Fetch all access packages
        List<AccessPackagesExport>? packages = await Platform.AccessManagementClient.GetAccessPackages();
        Assert.NotNull(packages);

        VendorTokenMaskinporten = Platform.GetMaskinportenTokenForVendor().Result;

        List<AccessPackageDto> relevantPackages = packages
            .SelectMany(category => category.Areas)
            .SelectMany(area => area.Packages)
            .ToList();

        //Creates System in System Register with these access packages
        string[] accessPackages =
        [
            "urn:altinn:accesspackage:akvakultur"
        ];

        var name = Guid.NewGuid().ToString();
        SystemId = await Platform.Common.CreateSystemWithAccessPackages(accessPackages, name);
    }

    public async Task DisposeAsync()
    {
        await Platform.Common.DeleteSystem(SystemId, VendorTokenMaskinporten);
    }
}