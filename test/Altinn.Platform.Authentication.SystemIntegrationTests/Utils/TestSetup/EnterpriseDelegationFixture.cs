using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class EnterpriseDelegationFixture : TestFixture, IAsyncLifetime
{
    public required string SystemId;
    public string? VendorTokenMaskinporten;
    public string? ClientId { get; set; }
    public List<SystemRegisterAccessPackageDto>? PostedPackages;

    public async Task InitializeAsync()
    {
        //Fetch all access packages
        List<AccessPackagesExport>? packagesFromAm = await Platform.AccessManagementClient.GetAccessPackages();
        Assert.NotNull(packagesFromAm);

        VendorTokenMaskinporten = Platform.GetMaskinportenTokenForVendor().Result;

        List<AccessPackageDto> relevantPackages = packagesFromAm
            .SelectMany(category => category.Areas)
            .SelectMany(area => area.Packages)
            .Where(package => !package.Urn.Contains("eksplisitt"))
            // .Where(package => package.IsAssignable)
            // .Where(package => package.IsDelegable)
            .ToList();

        //Creates System in System Register with these access packages
        PostedPackages = relevantPackages
            .Select(urn => new SystemRegisterAccessPackageDto { Urn = urn.Urn })
            .ToList();

        var name = Guid.NewGuid().ToString();
        SystemId = await Platform.Common.CreateSystemWithAccessPackages(PostedPackages, name);
    }

    public async Task DisposeAsync()
    {
        await Platform.Common.DeleteSystem(SystemId, VendorTokenMaskinporten);
    }
}