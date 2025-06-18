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
            "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet",
            "urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet",
            "urn:altinn:accesspackage:regnskapsforer-lonn",
            "urn:altinn:accesspackage:ansvarlig-revisor",
            "urn:altinn:accesspackage:revisormedarbeider",
            "urn:altinn:accesspackage:skattegrunnlag",
            "urn:altinn:accesspackage:forretningsforer-eiendom"
        ];

        SystemId = await Platform.Common.CreateSystemWithAccessPackages(accessPackages);
    }

    public async Task DisposeAsync()
    {
        await Platform.Common.DeleteSystem(SystemId, VendorTokenMaskinporten);
    }
    
   
    
}