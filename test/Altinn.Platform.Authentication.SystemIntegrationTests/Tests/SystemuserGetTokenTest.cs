using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

// Documentation: https://docs.digdir.no/docs/Maskinporten/maskinporten_func_systembruker
public class SystemuserGetTokenTest
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemuserGetTokenTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    [Fact]
    public async Task GetByExternalIdMaskinporten()
    {
        //Får ikke laget bruker med riktig scope hjer i samarbeidsportalen?? Why?
        // var maskinportenToken = await _platformClient.GetMaskinportenToken();
        const string scopes = "altinn:maskinporten/systemuser.read";

        var altinnEnterpriseToken =
            await _platformClient.GetEnterpriseAltinnToken(_platformClient.EnvironmentHelper.Vendor, scopes);

        var endpoint = "v1/systemuser/byExternalId";

        // Define the query parameters
        var clientId = _platformClient.EnvironmentHelper.maskinportenClientId;
        var systemProviderOrgNo = _platformClient.EnvironmentHelper.Vendor;
        var systemUserOwnerOrgNo = _platformClient.EnvironmentHelper.Vendor;
        // var externalRef = "51554df1-6013-4471-bf80-7281749bc042";
        

        // Build the query string
        var queryString =
            $"?clientId={clientId}&systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}";

        // Combine the endpoint and query string
        var fullEndpoint = $"{endpoint}{queryString}";

        // Make the GET request with the full endpoint and token
        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());
        Assert.NotNull(resp);
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrg()
    {
        var maskinportenToken = await _platformClient.GetSystemUserToken();
        _outputHelper.WriteLine($"maskinportenToken: {maskinportenToken}");
    }
}