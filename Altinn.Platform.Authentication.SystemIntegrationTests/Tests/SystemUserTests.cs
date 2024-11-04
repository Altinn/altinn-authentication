using System.Net;
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
[Trait("Category", "IntegrationTest")]
public class SystemUserTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformAuthentication;

    /// <summary>
    /// Testing System user endpoints
    /// </summary>
    /// 
    public SystemUserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformAuthentication = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformAuthentication);
    }

    private async Task<SystemRegisterState> CreateSystemRegisterUser()
    {
        // Prerequisite-step
        var token = await _platformAuthentication.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        await _systemRegisterClient.PostSystem(teststate);

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
            //scopes = "altinn:authentication/systemuser.request.read"
        };
        var token = await _platformAuthentication.GetPersonalAltinnToken(manager);

        //Act
        var respons =
            await _platformAuthentication.PostAsync("authentication/api/v1/systemuser/50692553", requestBody,
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

        var token = await _platformAuthentication.GetPersonalAltinnToken(manager);

        const string party = "50692553";
        const string endpoint = "authentication/api/v1/systemuser/" + party;

        var respons = await _platformAuthentication.GetAsync(endpoint, token);

        _outputHelper.WriteLine(await respons.Content.ReadAsStringAsync());
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

        var token = await _platformAuthentication.GetPersonalAltinnToken(manager);

        // Act
        var respons =
            await _platformAuthentication.Delete($"authentication/api/v1/systemuser/{party}/{id}", token);
        Assert.Equal(HttpStatusCode.Accepted, respons.StatusCode);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/574?issue=Altinn%7Caltinn-authentication%7C586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUser()
    {
        var token = await _platformAuthentication.GetTokenForClient("SystemRegisterClient");

        //Create system in System register
        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);
        
        var response = await _systemRegisterClient.PostSystem(teststate);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare
        var body = await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json");
        body = body.Replace("{systemId}", teststate.SystemId);

        // Act
        var respons =
            await _platformAuthentication.PostAsync("authentication/api/v1/systemuser/request/vendor", body, token);

        _outputHelper.WriteLine(await respons.Content.ReadAsStringAsync());

        var content = await respons.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == respons.StatusCode, $"Status code was not OK, but: {respons.StatusCode} -  {content}");
    }

    private async Task<HttpResponseMessage> CreateSystemUserTestdata(string party, AltinnUser user)
    {
        var teststate = await CreateSystemRegisterUser();

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");

        requestBody = requestBody
            .Replace("{systemId}", $"{teststate.SystemId}")
            .Replace("{randomIntegrationTitle}", $"{teststate.Name}");

        var endpoint = "authentication/api/v1/systemuser/" + party;

        var token = await _platformAuthentication.GetPersonalAltinnToken(user);

        var respons = await _platformAuthentication.PostAsync(endpoint, requestBody, token);
        return respons;
    }
}