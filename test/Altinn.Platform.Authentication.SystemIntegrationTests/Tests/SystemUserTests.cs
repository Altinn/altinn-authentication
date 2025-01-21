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

        var altinnToken = await _platformClient.GetPersonalAltinnToken(dagl);

        var endpoint = UrlConstants.GetSystemUserByPartyIdUrlTemplate.Replace("{partyId}", dagl.AltinnPartyId);

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
        _outputHelper.WriteLine(await response.Content.ReadAsStringAsync());

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

    /// "End to end" from creating request for System user to approving it and using /GET system user to find created user and deleting it
    [Fact]
    public async Task DeleteSystemUserTest()
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

        // Assert system user exists
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, content);

        // Extract system user ID
        var systemUserId = ExtractSystemUserId(content);

        // Act - Delete the system user
        await DeleteSystemUser(testperson.AltinnPartyId, systemUserId);

        // Assert - Verify system user is deleted
        var deleteVerificationResponse = await GetSystemUserById(systemId, maskinportenToken);

        Assert.Equal(HttpStatusCode.OK, deleteVerificationResponse.StatusCode);
        Assert.DoesNotContain(systemUserId, await deleteVerificationResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateEndToEndSystemUser()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemUser(maskinportenToken);
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

        // Extract system user ID
        var systemUserId = ExtractSystemUserId(await systemUserResponseContent.Content.ReadAsStringAsync());
        _outputHelper.WriteLine($"SystemUserId: {systemUserId}");

        // var stystemUserToken = await _platformClient.GetSystemUserToken();

        // // Delete system user 
        // await DeleteSystemUser(testperson.AltinnPartyId, systemUserId);
        //
        // // Delete system in System Register
        // var respons = await _platformClient.Delete(
        //     $"{UrlConstants.DeleteSystemRegister}/{systemId}", maskinportenToken);
    }

    [Fact]
    public async Task RemoveAllSystemUsersForEndtoEndTests()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var dagl = _platformClient.FindTestUserByRole("DAGL");
        var altinnToken = await _platformClient.GetPersonalAltinnToken(dagl);

        //Get All system users for party
        var endpoint = UrlConstants.GetSystemUserByPartyIdUrlTemplate.Replace("{partyId}", dagl.AltinnPartyId);

        var respons = await _platformClient.GetAsync(endpoint, altinnToken);
        var jsonResponse = await respons.Content.ReadAsStringAsync();
        //Parse into class SystemUser (list of)
        var systemUsers = JsonSerializer.Deserialize<List<SystemUser>>(jsonResponse, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.True(HttpStatusCode.OK == respons.StatusCode,
            $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");

        foreach (var systemUser in systemUsers.Where(systemUser => !systemUser.IntegrationTitle.Equals(
                                                                       "IntegrationTestNbTeam-Authentication-SystemuserE2E-User-Do-NoTt-Delete") &&
                                                                   systemUser.SupplierOrgno.Equals(_platformClient
                                                                       .EnvironmentHelper.Vendor)))
        {
            await DeleteSystemUser(dagl.AltinnPartyId, systemUser.Id.ToString());
            // Assert - Verify system user is deleted
            var deleteVerificationResponse = await GetSystemUserById(systemUser.SystemId, maskinportenToken);

            Assert.Equal(HttpStatusCode.OK, deleteVerificationResponse.StatusCode);
            Assert.DoesNotContain(systemUser.Id.ToString(),
                await deleteVerificationResponse.Content.ReadAsStringAsync());
        }
    }


    //Create system user with valid machineporten client id
    public async Task<string> CreateSystemUser(string maskinportenToken)
    {
        var name = "Team-Authentication-SystemuserE2E-User " + Guid.NewGuid();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(_platformClient.EnvironmentHelper
                .maskinportenClientId) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor) //Matches the maskinporten settings
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithName(name)
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = teststate.GenerateRequestBody();

        // Act
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var externalRef = Guid.NewGuid();

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", teststate.SystemId)
            .Replace("{externalRef}", externalRef.ToString())
            .Replace("{redirectUrl}", "https://altinn.no");

        var rightsJson = JsonSerializer.Serialize(teststate.Rights, new JsonSerializerOptions
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
        var url = UrlConstants.GetSystemUserRequestStatusUrlTemplate.Replace("requestId", requestId);
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
        var approveUrl = UrlConstants.ApproveSystemUserRequestUrlTemplate
            .Replace("{partyId}", altinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await ApproveRequest(approveUrl, GetTestUserForVendor());

        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK,
            $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    private async Task<HttpResponseMessage> GetSystemUserById(string systemId, string token)
    {
        var urlGetBySystem = UrlConstants.GetBySystemForVendor.Replace("{systemId}", systemId);
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
        var deleteUrl = UrlConstants.DeleteSystemUserUrlTemplate
            .Replace("{partyId}", altinnPartyId)
            .Replace("{systemUserId}", systemUserId);
        var deleteResponse = await DeleteRequest(deleteUrl, GetTestUserForVendor());

        Assert.Equal(HttpStatusCode.Accepted, deleteResponse.StatusCode);
    }
}