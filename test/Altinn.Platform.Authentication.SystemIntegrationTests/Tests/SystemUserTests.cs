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
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken);

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
        var systemInSystemRegister = await CreateSystemInSystemRegister(maskinportenToken);
        var systemUserResponse = await CreateSystemUserRequestWithExternalRef(systemInSystemRegister, maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var externalRef = Common.ExtractPropertyFromJson(systemUserResponse, "externalRef");
        var testperson = GetTestUserForVendor();

        // Act
        await _platformClient.ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await _platformClient.GetSystemUserBySystemIdForVendor(systemId, maskinportenToken);
        var responseByExternalRef = await GetSystemUserByExternalRef(externalRef, systemId, maskinportenToken);

        // Assert response codes
        await Common.AssertResponse(responseByExternalRef, HttpStatusCode.OK);
        await Common.AssertResponse(statusResponse, HttpStatusCode.OK);
        await Common.AssertResponse(systemUserResponseContent, HttpStatusCode.OK);

        // Assert actual content
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, await systemUserResponseContent.Content.ReadAsStringAsync());
        Assert.Contains(systemId, await responseByExternalRef.Content.ReadAsStringAsync());
        
        //Cleanup testdata
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();
        var systemUserId = Common.ExtractSystemUserId(content);
        await _platformClient.DeleteSystemUser(testperson.AltinnPartyId, systemUserId);
    }

    private async Task<HttpResponseMessage> GetSystemUserByExternalRef(string externalRef, string systemId, string maskinportenToken)
    {
        var urlGetBySystem = UrlConstants.GetByExternalRef
            .Replace("{externalRef}", externalRef)
            .Replace("{systemId}", systemId)
            .Replace("{orgNo}", _platformClient.EnvironmentHelper.Vendor);

        return await _platformClient.GetAsync(urlGetBySystem, maskinportenToken);
    }

    private async Task<SystemRegisterHelper> CreateSystemInSystemRegister(string maskinportenToken)
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
        return testState;
    }

    [Fact]
    public async Task DeleteSystemUserRequestTest()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken);
        var requestId = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var urlDelete = UrlConstants.DeleteRequest.Replace("{requestId}", requestId);

        // Act - Delete System User Request
        var responseDelete = await _platformClient.Delete(urlDelete, maskinportenToken);
        Assert.Equal(HttpStatusCode.Accepted, responseDelete.StatusCode);

        // Assert that System User request was deleted
        var statusResponse = await GetSystemUserRequestStatus(requestId, maskinportenToken);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
    }

    /// "End to end" from creating request for System user to approving it and using /GET system user to find created user and deleting it
    [Fact]
    public async Task DeleteSystemUserTest()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken, true);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = GetTestUserForVendor();

        await _platformClient.ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await _platformClient.GetSystemUserBySystemIdForVendor(systemId, maskinportenToken);
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();

        // Assert system user exists
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, content);

        // Extract system user ID
        var systemUserId = Common.ExtractSystemUserId(content);

        // Act - Delete the system user
        await _platformClient.DeleteSystemUser(testperson.AltinnPartyId, systemUserId);

        // Assert - Verify system user is deleted
        var deleteVerificationResponse = await _platformClient.GetSystemUserBySystemIdForVendor(systemId, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, deleteVerificationResponse.StatusCode);
        Assert.DoesNotContain(systemUserId, await deleteVerificationResponse.Content.ReadAsStringAsync());
    }

    public async Task<string> CreateSystemUserRequestWithExternalRef(SystemRegisterHelper testState, string maskinportenToken)
    {
        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequestExternalRef.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl)
            .Replace("{externalRef}", Guid.NewGuid().ToString());

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

    public async Task<string> CreateSystemAndSystemUserRequest(string maskinportenToken, bool withApp = false)
    {
        var testState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);
        if (withApp)
        {
            testState.Rights.Add(new Right
            {
                Resource = new List<Resource>
                {
                    new Resource
                    {
                        Id = "urn:altinn:resource",
                        Value = "app_ttd_endring-av-navn-v2"
                    }
                }
            });
        }

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
    
    [Fact]
    public async Task ApproveRequestSystemUserTest_WithApp()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken, true);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = GetTestUserForVendor();

        // Act
        await _platformClient.ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await _platformClient.GetSystemUserBySystemIdForVendor(systemId, maskinportenToken);

        // Assert
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, await systemUserResponseContent.Content.ReadAsStringAsync());
        
        //Cleanup
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();
        var systemUserId = Common.ExtractSystemUserId(content);
        await _platformClient.DeleteSystemUser(testperson.AltinnPartyId, systemUserId);
    }

    private async Task RegisterSystem(SystemRegisterHelper testState, string maskinportenToken)
    {
        var requestBodySystemRegister = testState.GenerateRequestBody();
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
    }


    public static async Task<string> PrepareSystemUserRequest(SystemRegisterHelper testState)
    {
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        var rightsJson = JsonSerializer.Serialize(testState.Rights, Helper.JsonSerializerOptions);
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

    public static async Task AssertSystemUserRequestStatus(HttpResponseMessage response, string expectedStatus)
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
}