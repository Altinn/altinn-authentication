using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

// Documentation: https://docs.digdir.no/docs/Maskinporten/maskinporten_func_systembruker
public class SystemuserGetTokenTest
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemUserClient _systemUserClient;
    private const string SystemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete";

    private static readonly JsonSerializerOptions? JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemuserGetTokenTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemUserClient = new SystemUserClient(_platformClient);
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    [Fact]
    public async Task GetByExternalIdMaskinporten()
    {
        // Setup
        var systemUser = await GetSystemUser(SystemId); 
                         // ?? await GetSystemUserWithProperMaskinportenClient();

        //Only way to use this token is by using the "fake" altinn token service, not allowed to configure this in samarbeidsportalen
        const string scopes = "altinn:maskinporten/systemuser.read";

        var altinnEnterpriseToken =
            await _platformClient.GetEnterpriseAltinnToken(_platformClient.EnvironmentHelper.Vendor, scopes);

        // Define the query parameters
        var clientId = _platformClient.EnvironmentHelper.maskinportenClientId;
        var systemProviderOrgNo = _platformClient.EnvironmentHelper.Vendor;
        var systemUserOwnerOrgNo = _platformClient.EnvironmentHelper.Vendor;
        var externalRef = systemUser?.ExternalRef;
        
        // _outputHelper.WriteLine($"System user external ref: {systemUser.ExternalRef}");

        // Build the query string
        var queryString =
            $"?clientId={clientId}" +
            $"&systemProviderOrgNo={systemProviderOrgNo}" +
            $"&systemUserOwnerOrgNo={systemUserOwnerOrgNo}" +
            $"&externalRef={externalRef}";

        // Combine the endpoint and query string
        var fullEndpoint = $"{UrlConstants.SystemUserGetByExternalRef}{queryString}";

        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());
        Assert.NotNull(resp);
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgWithExternalRef()
    {
        var systemUser = await GetSystemUser(SystemId) ?? await CreateSystemUserWithProperClient();
        var maskinportenToken = await _platformClient.GetSystemUserToken(systemUser?.ExternalRef);
        _outputHelper.WriteLine($"maskinportenToken: {maskinportenToken}");
    }
    
    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgNoExternalRef()
    {
        var systemUser = await GetSystemUser(SystemId) ?? await CreateSystemUserWithProperClient();
        var maskinportenToken = await _platformClient.GetSystemUserToken();
        _outputHelper.WriteLine($"maskinportenToken: {maskinportenToken}");
    }

    [Fact]
    public async Task SystemUserMaskinportenGetByExternalRef()
    {
        const string systemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete";

        if (await GetSystemUser(systemId) is null)
        {
            await CreateSystemUserWithProperClient();
        }
    }

    private async Task<SystemUser?> CreateSystemUserWithProperClient()
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");
        
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(_platformClient.EnvironmentHelper.maskinportenClientId) //Creates System User With MaskinportenClientId
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource")
            .WithName("Team-Authentication-SystemuserE2E-User-Do-Not-Delete-TT02")
            .WithToken(maskinportenToken);
        
        var requestBody = teststate.GenerateRequestBody();

        // Create system in System Register
        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Create system user with same created rights mentioned above
        var postSystemUserResponse = await _systemUserClient.CreateSystemUserRequestWithExternalRef(teststate, maskinportenToken);

        //Approve system user
        var id = Common.ExtractPropertyFromJson(postSystemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(postSystemUserResponse, "systemId");
        
        await _systemUserClient.ApproveSystemUserRequest(testuser, id);
        
        //Return system user and make sure it was created
        return await GetSystemUser(systemId);
    }

    private async Task<SystemUser?> GetSystemUser(string systemId)
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");

        var altinnToken = await _platformClient.GetPersonalAltinnToken(testuser);
        var resp = await _systemUserClient.GetSystemuserForParty(testuser.AltinnPartyId, altinnToken);

        var content = await resp.Content.ReadAsStringAsync();
        var systemUsers = JsonSerializer.Deserialize<List<SystemUser>>(content, JsonSerializerOptions) ?? [];

        return systemUsers.Find(user => user.SystemId == systemId);
    }
}