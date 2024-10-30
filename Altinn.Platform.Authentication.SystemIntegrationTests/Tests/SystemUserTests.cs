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
    private readonly PlatformAuthenticationClient _platformAuthenticationClient;

    /// <summary>
    /// Testing System user endpoints
    /// </summary>
    public SystemUserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformAuthenticationClient = new PlatformAuthenticationClient();
    }

    public async Task<SystemRegisterState> CreateSystemRegisterUser()
    {
        // Prerequisite-step
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        return teststate;
    }

    /// <summary>
    /// Verify that system is created
    /// </summary>
    [Fact]
    public async Task CreateSystemUser()
    {
        // Prerequisite
        var teststate = await CreateSystemRegisterUser();

        // Prepare
        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");

        requestBody = requestBody
            .Replace("{systemId}", $"{teststate.SystemId}")
            .Replace("{randomIntegrationTitle}", $"{teststate.Name}");

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

    private async Task<HttpResponseMessage> CreateSystemUserTestdata(string party, AltinnUser user)
    {
        var teststate = await CreateSystemRegisterUser();

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");

        requestBody = requestBody
            .Replace("{systemId}", $"{teststate.SystemId}")
            .Replace("{randomIntegrationTitle}", $"{teststate.Name}");

        var endpoint = "authentication/api/v1/systemuser/" + party;

        var token = await _platformAuthenticationClient.GetPersonalAltinnToken(user);

        var respons = await _platformAuthenticationClient.PostAsync(endpoint, requestBody, token);
        return respons;
    }
}