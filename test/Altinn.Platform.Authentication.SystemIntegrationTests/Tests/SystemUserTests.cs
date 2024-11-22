using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
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

    /// <summary>
    /// Github: #https://github.com/Altinn/altinn-authentication/issues/765
    /// Test Get endpoint for System User
    /// </summary>
    [Fact]
    public async Task GetCreatedSystemUser()
    {
        var dagl = _platformClient.FindTestUserByRole("DAGL");
        //dagl.Scopes = "users.read";
        var altinnToken = await _platformClient.GetPersonalAltinnToken(dagl);

        var endpoint = "v1/systemuser/" + dagl.AltinnPartyId;

        var respons = await _platformClient.GetAsync(endpoint, altinnToken);

        Assert.True(HttpStatusCode.OK == respons.StatusCode,
            $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");
    }


    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/586
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
        var respons = await CreateSystemUserRequest(maskinportenToken);

        using var jsonDoc = JsonDocument.Parse(respons);
        var id = jsonDoc.RootElement.GetProperty("id").GetString();

        var url = $"v1/systemuser/request/vendor/{id}";

        var resp = await _platformClient.GetAsync(url, maskinportenToken);

        var status = jsonDoc.RootElement.GetProperty("status").GetString();
        Assert.True(status != null, $"Unable to find status in response {await resp.Content.ReadAsStringAsync()}");
        Assert.True(status.Equals("New"), "Unexpected status code: " + status);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>
    /// "End to end" from creating request for System user to approving it and using /GET system user to find created user
    /// </summary>
    [Fact]
    public async Task ApproveRequestSystemUser()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        // Create system and system user request
        var responseRequestSystemUser = await CreateSystemUserRequest(maskinportenToken);

        using var jsonDocSystemRequestResponse = JsonDocument.Parse(responseRequestSystemUser);
        var id = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();

        var vendor = _platformClient.EnvironmentHelper.Vendor;

        var testperson = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(vendor))
                         ?? throw new Exception($"Test user not found for organization: {vendor}");

        // Approve
        var approveResp =
            await ApproveRequest($"v1/systemuser/request/{testperson.AltinnPartyId}/{id}/approve", testperson);
        Assert.True(HttpStatusCode.OK == approveResp.StatusCode);

        //Get status
        var url = $"v1/systemuser/request/vendor/{id}";
        var resp = await _platformClient.GetAsync(url, maskinportenToken);

        using var jsonDocRequestStatus = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var status = jsonDocRequestStatus.RootElement.GetProperty("status").GetString();
        Assert.True(status != null, $"Unable to find status in response {await resp.Content.ReadAsStringAsync()}");
        Assert.True(status.Equals("Accepted"), "Status was not approved but: " + status);

        var systemId = jsonDocSystemRequestResponse.RootElement.GetProperty("systemId").GetString();
        Assert.True(systemId != null, $"Unable to find system user id {systemId}");

        //Verify system is found
        var urlGetBySystem = $"v1/systemuser/vendor/bysystem/{systemId}";
        var responseGetBySystem = await _platformClient.GetAsync(urlGetBySystem, maskinportenToken);
        Assert.Contains(systemId, responseGetBySystem.Content.ReadAsStringAsync().Result);

        Assert.True(1 == 2, "you failed :(");
    }


    public async Task<string> CreateSystemUserRequest(string maskinportenToken)
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

    private async Task<HttpResponseMessage> ApproveRequest(string endpoint, Testuser testperson)
    {
        // Get the Altinn token
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await _platformClient.PostAsync(endpoint, string.Empty, altinnToken);
        return response;
    }
}