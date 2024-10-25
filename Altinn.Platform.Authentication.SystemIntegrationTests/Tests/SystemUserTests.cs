using System.Net;
using System.Text.Json;
using Altinn.AccessManagement.SystemIntegrationTests.Domain;
using Altinn.AccessManagement.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Class containing system user tests
/// </summary>
public class SystemUserTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformAuthenticationClient;
    private readonly MaskinPortenTokenGenerator _maskinPortenTokenGenerator;

    /// <summary>
    /// Testing System user endpoints
    /// </summary>
    public SystemUserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _systemRegisterClient = new SystemRegisterClient(_outputHelper);
        _platformAuthenticationClient = new PlatformAuthenticationClient();
        _maskinPortenTokenGenerator = new MaskinPortenTokenGenerator();
    }

    /// <summary>
    /// Verify that system is created
    /// </summary>
    [Fact]
    public async Task CreateSystemUser()
    {
        // Prepare
        var maskinportenToken = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        // the vendor of the system, could be visma
        const string vendorId = "312605031";
        var randomName = Helper.GenerateRandomString(15);

        var testfile = await Helper.ReadFile("Resources/Testdata/Systemregister/CreateNewSystem.json");

        testfile = testfile
            .Replace("{vendorId}", vendorId)
            .Replace("{randomName}", randomName)
            .Replace("{clientId}", Guid.NewGuid().ToString());

        RegisterSystemRequest? systemRequest =
            System.Text.Json.JsonSerializer.Deserialize<RegisterSystemRequest>(testfile);
        await _systemRegisterClient.CreateNewSystem(systemRequest!, maskinportenToken, vendorId);

        var response =
            await _platformAuthenticationClient.GetAsync(
                $"/authentication/api/v1/systemregister/{vendorId}_{randomName}/", maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");
        requestBody = requestBody
            .Replace("{systemId}", $"{vendorId}_{randomName}")
            .Replace("{randomIntegrationTitle}", $"{randomName}");

        var manager = new AltinnUser
        {
            userId = "20012772", partyId = "51670464", pid = "64837001585",
            scopes = "altinn:authentication/systemuser.request.read"
        };
        var token = await _platformAuthenticationClient.GetPersonalAltinnToken(manager);

        //Act
        var respons =
            await _platformAuthenticationClient.PostAsync("authentication/api/v1/systemuser/50692553", requestBody,
                token);

        //Assert
        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
    }

    /// <summary>
    /// Test Get endpoint for System User
    /// Github: #765
    /// </summary>
    [Fact]
    public async Task GetCreatedSystemUser()
    {
        const string alternativeParty = "50891151";

        var manager = new AltinnUser
        {
            userId = "20012772", partyId = alternativeParty, pid = "04855195742",
            scopes = "altinn:authentication" //What use does this even have
        };

        var token = await _platformAuthenticationClient.GetPersonalAltinnToken(manager);

        const string party = "50692553";
        const string endpoint = "authentication/api/v1/systemuser/" + party;

        var respons = await _platformAuthenticationClient.GetAsync(endpoint, token);
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    /// <summary>
    /// Test Delete for System User
    /// Github: Todo
    /// </summary>
    [Fact]
    public async Task DeleteCreatedSystemUser()
    {
        //Prepare
        const string party = "50692553";
        var manager = new AltinnUser
        {
            userId = "20012772", partyId = "51670464", pid = "64837001585",
            scopes = "altinn:authentication/systemuser.request.read"
        };

        var jsonObject =
            JObject.Parse(await (await CreateSystemUserTestdata(party, manager)).Content.ReadAsStringAsync());
        var
            id = jsonObject[
                "id"]; //SystemId -//Todo: Why is "id" the same as systemuserid in Swagger? Confusing to mix with "systemid"

        var token = await _platformAuthenticationClient.GetPersonalAltinnToken(manager);

        // Act
        var respons =
            await _platformAuthenticationClient.Delete($"authentication/api/v1/systemuser/{party}/{id}", token);
        Assert.Equal(HttpStatusCode.Accepted, respons.StatusCode);
    }

    public async Task<HttpResponseMessage> CreateSystemUserTestdata(string party, AltinnUser user)
    {
        // Prepare
        var maskinportenToken = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");
        
        // the vendor of the system, could be visma
        const string vendorId = "312605031";
        var randomName = Helper.GenerateRandomString(15);

        var testfile = await Helper.ReadFile("Resources/Testdata/Systemregister/CreateNewSystem.json");

        testfile = testfile
            .Replace("{vendorId}", vendorId)
            .Replace("{randomName}", randomName)
            .Replace("{clientId}", Guid.NewGuid().ToString());

        var systemRequest =
            JsonSerializer.Deserialize<RegisterSystemRequest>(testfile);
        await _systemRegisterClient.CreateNewSystem(systemRequest!, maskinportenToken, vendorId);

        var response =
            await _platformAuthenticationClient.GetAsync(
                $"/authentication/api/v1/systemregister/{vendorId}_{randomName}/", maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");
        requestBody = requestBody
            .Replace("{systemId}", $"{vendorId}_{randomName}")
            .Replace("{randomIntegrationTitle}", $"{randomName}");

        var endpoint = "authentication/api/v1/systemuser/" + party;

        var token = await _platformAuthenticationClient.GetPersonalAltinnToken(user);
        //Act
        var respons = await _platformAuthenticationClient.PostAsync(endpoint, requestBody, token);
        return respons;
    }
}