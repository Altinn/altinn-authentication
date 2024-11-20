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
    /// Reported bug: https://github.com/Altinn/altinn-authentication/issues/848
    /// </summary>
    // [Fact]
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

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/791
    /// API for creating a change request for System User
    /// </summary>
    [Fact]
    public async Task PostChangeRequestSystemUserAndApproveReturnSuccess()
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

        // Prepare New Request for a new SystemUser from a Vendor
        var body = await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json");
        body = body
            .Replace("{systemId}", teststate.SystemId)
            .Replace("{redirectUrl}", teststate.RedirectUrl);

        var respons =
            await _platformClient.PostAsync("authentication/api/v1/systemuser/request/vendor", body,
                maskinportenToken);

        var content = await respons.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == respons.StatusCode,
            $"Status code was not Created, but: {respons.StatusCode} -  {content}");

        // Prepare Approve Request by end user
        var requestId = JObject.Parse(content)["id"].ToString();

        const string party = "50692553";
        var manager = new AltinnUser
        {
            userId = "20012772",
            partyId = "51670464",
            pid = "64837001585",
        };

        var token = await _platformClient.GetPersonalAltinnToken(manager);

        // End user approves the request
        var responseApprove = await _platformClient.PostAsync($"authentication/api/v1/systemuser/request/{party}/{requestId}/approve", null!, token);
        Assert.True(HttpStatusCode.OK == responseApprove.StatusCode, $"Status code was not OK, but: {responseApprove.StatusCode} -  {await responseApprove.Content.ReadAsStringAsync()}");

        // Prepare Create Change Request for an existing SystemUser by a Vendor
        var bodyChange = await Helper.ReadFile("Resources/Testdata/SystemUser/CreateChangeRequest.json");

        bodyChange = bodyChange
            .Replace("{systemId}", teststate.SystemId)
            .Replace("{redirectUrl}", teststate.RedirectUrl);

        // Use the Verify endpoint to test if the change request returns an OK empty response, ie no change needed
        var responsChange = await _platformClient.PostAsync("authentication/api/v1/systemuser/changerequest/vendor/verify", body, maskinportenToken);
        Assert.True(HttpStatusCode.OK == responsChange.StatusCode, $"Status code was not Ok, but: {responsChange.StatusCode} -  {await responsChange.Content.ReadAsStringAsync()}");

        // Use the Verify endpoint to test if the change request returns a set of Required Rights, because the change is needed
        var responsChangeNeeded = await _platformClient.PostAsync("authentication/api/v1/systemuser/changerequest/vendor/verify", bodyChange, maskinportenToken);

        Assert.True(HttpStatusCode.OK == responsChangeNeeded.StatusCode, $"Status code was not OK, but: {responsChangeNeeded.StatusCode} -  {await responsChangeNeeded.Content.ReadAsStringAsync()}");
        string changeRequestResponse = JObject.Parse(await responsChangeNeeded.Content.ReadAsStringAsync()).ToString();
        string requiredRights = JObject.Parse(changeRequestResponse)["requiredRights"].ToString();

        // Use the Create endpoint to create the change request, returns a ChangeRequestResponse
        var responsChangeCreate = await _platformClient.PostAsync("authentication/api/v1/systemuser/changerequest/vendor", bodyChange, maskinportenToken);

        Assert.True(HttpStatusCode.Created == responsChangeCreate.StatusCode, $"Status code was not OK, but: {responsChangeCreate.StatusCode} -  {await responsChangeCreate.Content.ReadAsStringAsync()}");
        string changeRequestResponseCreated = JObject.Parse(await responsChangeCreate.Content.ReadAsStringAsync()).ToString();
        string requestIdChange = JObject.Parse(changeRequestResponseCreated)["id"].ToString();
        Assert.NotEmpty(requestIdChange);

        var responseApproveChange = await _platformClient.PostAsync($"authentication/api/v1/systemuser/changerequest/{party}/{requestIdChange}/approve", null!, token);
        Assert.True(HttpStatusCode.OK == responseApproveChange.StatusCode, $"Status code was not OK, but: {responseApproveChange.StatusCode} -  {await responseApproveChange.Content.ReadAsStringAsync()}");
    }


    /// <summary>
    /// https://docs.altinn.studio/nb/authentication/guides/systemauthentication-for-systemproviders/
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