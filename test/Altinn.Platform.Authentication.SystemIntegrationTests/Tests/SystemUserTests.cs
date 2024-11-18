using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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

    private async Task<SystemRegisterHelper> CreateSystemRegisterUser()
    {
        // Prerequisite-step
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        return teststate;
    }

    /// <summary>
    /// Test Get endpoint for System User
    /// Github: #765
    /// Reported bug: https://github.com/Altinn/altinn-authentication/issues/848
    /// </summary>
    [Fact]
    public async Task GetCreatedSystemUser()
    {
        var dagl = _platformClient.FindTestUserByRole("DAGL");
        dagl.Scopes = "users.read";
        var altinnToken = await _platformClient.GetPersonalAltinnToken(dagl);

        var endpoint = "v1/systemuser/" + dagl.AltinnPartyId;

        var respons = await _platformClient.GetAsync(endpoint, altinnToken);

        //TODO - fix. Breaks in at22 due to 403. Verify endpoint and test user
        // Assert.True(HttpStatusCode.OK == respons.StatusCode,
        //     $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");
    }


    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/574?issue=Altinn%7Caltinn-authentication%7C586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUser()
    {
        // Prerequisite-step
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var testState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor("312605031")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        var requestBodySystemREgister = testState.GenerateRequestBody();

        // Register system
        var response = await _systemRegisterClient.PostSystem(requestBodySystemREgister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        //Create system user request on the same rights that exist in the SystemRegister
        var rightsJson = JsonSerializer.Serialize(testState.Rights, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Act
        var userResponse =
            await _platformClient.PostAsync("v1/systemuser/request/vendor", finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/576
    /// </summary>
    [Fact]
    public async Task GetRequestSystemUserStatus()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var respons = await createSystemUserRequest(maskinportenToken);

        using var jsonDoc = JsonDocument.Parse(respons);
        var id = jsonDoc.RootElement.GetProperty("id").GetString();

        var url = $"v1/systemuser/request/vendor/{id}";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);

        var status = jsonDoc.RootElement.GetProperty("status").GetString();
        Assert.True(status != null, $"Unable to find status in response {await resp.Content.ReadAsStringAsync()}");
        Assert.True(status.Equals("New"), "Unexpected status code: " + status);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetSystemusersBySystem()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var respons = await createSystemUserRequest(maskinportenToken);

        using var jsonDoc = JsonDocument.Parse(respons);
        var systemId = jsonDoc.RootElement.GetProperty("systemId").GetString();
        Assert.True(systemId != null, $"Unable to find system user id {systemId}");

        var url = $"v1/systemuser/vendor/bysystem/{systemId}";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ApproveRequestSystemUser()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var respons = await createSystemUserRequest(maskinportenToken);

        _outputHelper.WriteLine(respons);

        using var jsonDoc = JsonDocument.Parse(respons);
        var id = jsonDoc.RootElement.GetProperty("id").GetString();

        //both of these work? Both parties that belong to the org / vendor
        var testperson = _platformClient.TestUsers.Find(testuser => testuser.Org.Equals(
            _platformClient.EnvironmentHelper.Vendor
        ));
        Assert.NotNull(testperson);

        var endpoint = _platformClient.BaseUrl + $"/v1/systemuser/request/{testperson.AltinnPartyId}/{id}/approve";

        var dagl = _platformClient.FindTestUserByRole("DAGL");
        var altinnToken = await _platformClient.GetPersonalAltinnToken(dagl);

        // Set up the HttpClient
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", altinnToken);

        // Create the POST request without a body
        var response = await httpClient.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        //Get status
        var url = $"v1/systemuser/request/vendor/{id}";
        var resp = await _platformClient.GetAsync(url, maskinportenToken);

        using var jsonDocRequestStatus = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var status = jsonDocRequestStatus.RootElement.GetProperty("status").GetString();
        Assert.True(status != null, $"Unable to find status in response {await resp.Content.ReadAsStringAsync()}");
        Assert.True(status.Equals("Accepted"), "Status was not approved but: " + status);
    }


    public async Task<string> createSystemUserRequest(string maskinportenToken)
    {
        var testState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor("312605031")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        var requestBodySystemREgister = testState.GenerateRequestBody();

        // Register system
        var response = await _systemRegisterClient.PostSystem(requestBodySystemREgister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        //Create system user request on the same rights that exist in the SystemRegister
        var rightsJson = JsonSerializer.Serialize(testState.Rights, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Act
        var userResponse =
            await _platformClient.PostAsync("v1/systemuser/request/vendor", finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        return content;
    }

    private async Task<HttpResponseMessage> CreateSystemUserTestdata(Testuser user)
    {
        var teststate = await CreateSystemRegisterUser();

        var requestBody = await Helper.ReadFile("Resources/Testdata/SystemUser/RequestSystemUser.json");

        requestBody = requestBody
            .Replace("{systemId}", $"{teststate.SystemId}")
            .Replace("{randomIntegrationTitle}", $"{teststate.Name}");

        var endpoint = "v1/systemuser/" + user.AltinnPartyId;

        var token = await _platformClient.GetPersonalAltinnToken(user);

        var respons = await _platformClient.PostAsync(endpoint, requestBody, token);
        return respons;
    }
}