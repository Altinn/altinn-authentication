using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        var endpoint = string.Format(UrlConstants.GetSystemUserByPartyIdUrlTemplate, dagl.AltinnPartyId);
        var respons = await _platformClient.GetAsync(endpoint, altinnToken);

        Assert.True(HttpStatusCode.OK == respons.StatusCode,
            $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUserTest()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        // Registering system to System Register
        var testState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        await RegisterSystem(testState, maskinportenToken);

        var requestBody = await PrepareSystemUserRequest(testState);

        // Act
        var userResponse = await
            _platformClient.PostAsync(UrlConstants.CreateSystemUserRequestBaseUrl, requestBody, maskinportenToken);

        // Assert
        await AssertSystemUserRequestCreated(userResponse);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/576
    /// </summary>
    [Fact]
    public async Task GetRequestSystemUserStatus()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemUserRequest(maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");

        // Act
        var response = await GetSystemUserRequestStatus(id, maskinportenToken);

        // Assert
        await AssertSystemUserRequestStatus(response, "New");
    }

    [Fact]
    public async Task ApproveRequestSystemUserTest()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemUserRequest(maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = GetTestUserForVendor();

        // Act
        await ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await GetSystemUserById(systemId, maskinportenToken);

        // Assert
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, await systemUserResponseContent.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// "End to end" from creating request for System user to approving it and using /GET system user to find created user and deleting it
    /// </summary>
    [Fact]
    public async Task DeleteSystemUserTest()
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

        // Ensure the response is successful
        responseGetBySystem.EnsureSuccessStatusCode();

        // Read the response content as a string
        var jsonString = await responseGetBySystem.Content.ReadAsStringAsync();

        // Parse the JSON response
        JsonNode jsonNode = JsonNode.Parse(jsonString);

        string systemUserId = string.Empty;
        // Check if it's an array
        if (jsonNode is JsonObject jsonObject)
        {
            // Get the element with data
            JsonArray jsonArray = jsonObject.ElementAt(1).Value.AsArray();
            JsonObject systemUserObject = jsonArray.First().AsObject();

            // Extract the 'id' of the created systemuser
            systemUserId = systemUserObject["id"].GetValue<string>();
        }

        //Delete
        var deleteUrl = string.Format(UrlConstants.DeleteSystemUserUrlTemplate, testperson.AltinnPartyId, systemUserId);
        var deleteResp = await DeleteRequest(deleteUrl, testperson);
        Assert.Equal(HttpStatusCode.Accepted, deleteResp.StatusCode);
    }

    [Fact]
    public async Task deleteRefactored()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemUserRequest(maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = GetTestUserForVendor();

        await ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await GetSystemUserById(systemId, maskinportenToken);
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();
        
        _outputHelper.WriteLine(systemId);
        _outputHelper.WriteLine(content);

        // Assert
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId,content);

        // Extract system user ID
        var systemUserId = ExtractSystemUserId(content);

        // Act - Delete the system user
        await DeleteSystemUser(testperson.AltinnPartyId, systemUserId);
    }


    public async Task<string> CreateSystemUserRequest(string maskinportenToken)
    {
        var testState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
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

    private async Task<HttpResponseMessage> DeleteRequest(string endpoint, Testuser testperson)
    {
        // Get the Altinn token
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await _platformClient.Delete(endpoint, altinnToken);
        return response;
    }

    private async Task RegisterSystem(SystemRegisterHelper testState, string maskinportenToken)
    {
        var requestBodySystemRegister = testState.GenerateRequestBody();
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
    }


    private async Task<string> PrepareSystemUserRequest(SystemRegisterHelper testState)
    {
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        var rightsJson = JsonSerializer.Serialize(testState.Rights, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");
    }

    private async Task AssertSystemUserRequestCreated(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {response.StatusCode} - {content}");
    }

    private async Task<HttpResponseMessage> GetSystemUserRequestStatus(string requestId, string token)
    {
        var url = string.Format(UrlConstants.GetSystemUserRequestStatusUrlTemplate, requestId);
        return await _platformClient.GetAsync(url, token);
    }

    private async Task AssertSystemUserRequestStatus(HttpResponseMessage response, string expectedStatus)
    {
        var responseContent = await response.Content.ReadAsStringAsync();
        var actualStatus = Common.ExtractPropertyFromJson(responseContent, "status");

        Assert.True(actualStatus != null, $"Unable to find status in response: {responseContent}");
        Assert.True(actualStatus.Equals(expectedStatus), $"Unexpected status: {actualStatus}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Testuser GetTestUserForVendor()
    {
        var vendor = _platformClient.EnvironmentHelper.Vendor;

        return _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(vendor))
               ?? throw new Exception($"Test user not found for organization: {vendor}");
    }

    private async Task ApproveSystemUserRequest(string altinnPartyId, string requestId)
    {
        var approveUrl = string.Format(UrlConstants.ApproveSystemUserRequestUrlTemplate, altinnPartyId, requestId);
        var approveResponse = await ApproveRequest(approveUrl, GetTestUserForVendor());

        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK,
            $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    private async Task<HttpResponseMessage> GetSystemUserById(string systemId, string token)
    {
        var urlGetBySystem = string.Format(UrlConstants.GetBySystemForVendor, systemId);
        return await _platformClient.GetAsync(urlGetBySystem, token);
    }

    private string ExtractSystemUserId(string jsonResponse)
    {
        var jsonNode = JsonNode.Parse(jsonResponse);

        if (jsonNode is JsonObject jsonObject && jsonObject.ElementAt(1).Value is JsonArray jsonArray)
        {
            var systemUserObject = jsonArray.First().AsObject();
            return systemUserObject["id"].GetValue<string>();
        }

        throw new Exception("Unable to extract system user ID from response.");
    }

    private async Task DeleteSystemUser(string altinnPartyId, string systemUserId)
    {
        var deleteUrl = string.Format(UrlConstants.DeleteSystemUserUrlTemplate, altinnPartyId, systemUserId);
        var deleteResponse = await DeleteRequest(deleteUrl, GetTestUserForVendor());

        Assert.Equal(HttpStatusCode.Accepted, deleteResponse.StatusCode);
    }
}