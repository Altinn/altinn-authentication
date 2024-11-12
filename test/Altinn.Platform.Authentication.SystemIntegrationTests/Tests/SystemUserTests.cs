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
    private readonly PlatformAuthenticationClient _platformClient;

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemUserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    private async Task<SystemRegisterState> CreateSystemRegisterUser()
    {
        // Prerequisite-step
        var maskinportenToken = await _platformClient.GetToken();

        var teststate = new SystemRegisterState()
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        await _systemRegisterClient.PostSystem(teststate);

        return teststate;
    }

    /// <summary>
    /// Verify that system is created
    /// </summary>
    [Fact]
    public async Task CreateSystemUserBff()
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
        var token = await _platformClient.GetPersonalAltinnToken(manager);

        //Act
        var respons =
            await _platformClient.PostAsync("authentication/api/v1/systemuser/50692553", requestBody,
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

        var token = await _platformClient.GetPersonalAltinnToken(manager);

        const string party = "50692553";
        const string endpoint = "authentication/api/v1/systemuser/" + party;

        var respons = await _platformClient.GetAsync(endpoint, token);

        Assert.True(HttpStatusCode.OK == respons.StatusCode,
            $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");
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
        };

        var jsonObject =
            JObject.Parse(await (await CreateSystemUserTestdata(party, manager)).Content.ReadAsStringAsync());
        var
            id = jsonObject[
                "id"]; //SystemId -//Todo: Why is "id" the same as systemuserid in Swagger? Confusing to mix with "systemid"

        var token = await _platformClient.GetPersonalAltinnToken(manager);

        // Act
        var respons =
            await _platformClient.Delete($"authentication/api/v1/systemuser/{party}/{id}", token);
        Assert.Equal(HttpStatusCode.Accepted, respons.StatusCode);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/574?issue=Altinn%7Caltinn-authentication%7C586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUser()
    {
        // Prerequisite-step
        var maskinportenToken = await _platformClient.GetToken();

        var teststate = new SystemRegisterState()
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        var response = await _systemRegisterClient.PostSystem(teststate);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare
        var body = await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json");
        body = body
            .Replace("{systemId}", teststate.SystemId)
            .Replace("{redirectUrl}", teststate.RedirectUrl);

        // Act
        var respons =
            await _platformClient.PostAsync("authentication/api/v1/systemuser/request/vendor", body,
                maskinportenToken);

        var content = await respons.Content.ReadAsStringAsync();
        _outputHelper.WriteLine(content);
        Assert.True(HttpStatusCode.Created == respons.StatusCode,
            $"Status code was not Created, but: {respons.StatusCode} -  {content}");
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/576
    /// </summary>
    [Fact]
    public async Task GetRequestSystemUserStatus()
    {
        var maskinportenToken = await _platformClient.GetToken();
        const string url = "/authentication/api/v1/systemuser/request/vendor/96034ac3-fc2d-4e72-887a-c72092e790b8";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetSystemusersBySystem()
    {
        var maskinportenToken = await _platformClient.GetToken();
        
        const string url = "authentication/api/v1/systemuser/vendor/bysystem/312605031_b4fadafa-42c5-44b6-88cc-ee2db237c4c0";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);
        var ok = await resp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
    
    [Fact]
    public async Task GetSystemUserByParty()
    {
        var maskinportenToken = await _platformClient.GetToken();
        
        const string url = "authentication/api/v1/systemuser/party/312605031";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);
        var ok = await resp.Content.ReadAsStringAsync();
        _outputHelper.WriteLine(ok);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>
    /// Todo: Implement
    /// </summary>
    [Fact]
    public async Task ApproveSystemUser()
    {
        var url = "/authentication/api/v1/systemuser/request/{party}/{requestId}/approve";
    }

    /// <summary>
    /// https://docs.altinn.studio/nb/authentication/guides/systemauthentication-for-systemproviders/
    /// Du trenger ikke angi system user
    /// </summary>
    //[Fact]
    public async Task UseSystemUser()
    {
        //Bruk jwt og hent maskinporten-token direkte
    }

    private async Task<HttpResponseMessage> CreateSystemUserTestdata(string party, AltinnUser user)
    {
        var teststate = await CreateSystemRegisterUser();

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");

        requestBody = requestBody
            .Replace("{systemId}", $"{teststate.SystemId}")
            .Replace("{randomIntegrationTitle}", $"{teststate.Name}");

        var endpoint = "authentication/api/v1/systemuser/" + party;

        var token = await _platformClient.GetPersonalAltinnToken(user);

        var respons = await _platformClient.PostAsync(endpoint, requestBody, token);
        return respons;
    }
}