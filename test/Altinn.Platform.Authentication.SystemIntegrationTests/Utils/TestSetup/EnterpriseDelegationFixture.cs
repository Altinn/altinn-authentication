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
            "urn:altinn:accesspackage:a-ordning",
            "urn:altinn:accesspackage:aksjer-og-eierforhold",
            "urn:altinn:accesspackage:akvakultur",
            "urn:altinn:accesspackage:annen-tjenesteyting",
            "urn:altinn:accesspackage:ansettelsesforhold",
            "urn:altinn:accesspackage:ansvarlig-revisor",
            "urn:altinn:accesspackage:attester",
            "urn:altinn:accesspackage:avfall-behandle-gjenvinne",
            "urn:altinn:accesspackage:baerekraft",
            "urn:altinn:accesspackage:barnehageeier",
            "urn:altinn:accesspackage:dokumentbasert-tilsyn",
            "urn:altinn:accesspackage:eksplisitt",
            "urn:altinn:accesspackage:finansiering-og-forsikring",
            "urn:altinn:accesspackage:folkeregister",
            "urn:altinn:accesspackage:forretningsforer-eiendom",
            "urn:altinn:accesspackage:forskning",
            "urn:altinn:accesspackage:generelle-helfotjenester",
            "urn:altinn:accesspackage:helfo-saerlig-kategori",
            "urn:altinn:accesspackage:informasjon-og-kommunikasjon",
            "urn:altinn:accesspackage:infrastruktur",
            "urn:altinn:accesspackage:kreditt-og-oppgjoer",
            "urn:altinn:accesspackage:krav-og-utlegg",
            "urn:altinn:accesspackage:livssynsorganisasjoner",
            "urn:altinn:accesspackage:lonn",
            "urn:altinn:accesspackage:maskinlesbare-hendelser",
            "urn:altinn:accesspackage:maskinporten-scopes",
            "urn:altinn:accesspackage:maskinporten-scopes-nuf",
            "urn:altinn:accesspackage:merverdiavgift",
            "urn:altinn:accesspackage:mine-sider-kommune",
            "urn:altinn:accesspackage:miljorydding-miljorensing-og-lignende",
            "urn:altinn:accesspackage:motorvognavgift",
            "urn:altinn:accesspackage:ordinaer-post-til-virksomheten",
            "urn:altinn:accesspackage:overnatting",
            "urn:altinn:accesspackage:patent-varemerke-design",
            "urn:altinn:accesspackage:pensjon",
            "urn:altinn:accesspackage:permisjon",
            "urn:altinn:accesspackage:personvernombud",
            "urn:altinn:accesspackage:politi-og-domstol",
            "urn:altinn:accesspackage:post-og-telekommunikasjon",
            "urn:altinn:accesspackage:post-til-virksomheten-med-taushetsbelagt-innhold",
            "urn:altinn:accesspackage:rapportering-statistikk",
            "urn:altinn:accesspackage:regnskap-okonomi-rapport",
            "urn:altinn:accesspackage:regnskapsforer-lonn",
            "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet",
            "urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet",
            "urn:altinn:accesspackage:renovasjon",
            "urn:altinn:accesspackage:reviorattesterer",
            "urn:altinn:accesspackage:revisormedarbeider",
            "urn:altinn:accesspackage:saeravgifter",
            "urn:altinn:accesspackage:servering",
            "urn:altinn:accesspackage:skattegrunnlag",
            "urn:altinn:accesspackage:skatt-naering",
            "urn:altinn:accesspackage:skoleeier",
            "urn:altinn:accesspackage:starte-drive-endre-avikle-virksomhet",
            "urn:altinn:accesspackage:sykefravaer",
            "urn:altinn:accesspackage:tilskudd-stotte-erstatning",
            "urn:altinn:accesspackage:toll",
            "urn:altinn:accesspackage:ulykke",
            "urn:altinn:accesspackage:varehandel",
            "urn:altinn:accesspackage:yrkesskade"
        ];

        SystemId = await Platform.Common.CreateSystemWithAccessPackages(accessPackages);
    }

    public async Task DisposeAsync()
    {
        await Platform.Common.DeleteSystem(SystemId, VendorTokenMaskinporten);
    }
}