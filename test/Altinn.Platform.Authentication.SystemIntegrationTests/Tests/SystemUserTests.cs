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
public class SystemUserTests : IDisposable
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly SystemUserClient _systemUserClient;
    private readonly PlatformAuthenticationClient _platformClient;
    private string? _systemUserId;
    private Testuser? _testperson;
    private Common _common;

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemUserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemUserClient = new SystemUserClient(_platformClient);
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
        _common = new Common(_platformClient, outputHelper);
    }
    
    
    // https://github.com/Altinn/altinn-authentication/issues/1123
    [Fact]
    public async Task TestRedirectUrlCase()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid().ToString();
        var testperson = _platformClient.GetTestUserForVendor();
        await _common.CreateRequestWithManalExample(maskinportenToken, externalRef, testperson, clientId);
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

        var endpoint = ApiEndpoints.GetSystemUsersByParty.Url().Replace("{party}", dagl.AltinnPartyId);

        var respons = await _platformClient.GetAsync(endpoint, altinnToken);

        Assert.True(HttpStatusCode.OK == respons.StatusCode, $"Received status code: {respons.StatusCode} more details: {await respons.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUserTest()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        // Registering system to System Register
        var testState = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
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
            _platformClient.PostAsync(ApiEndpoints.CreateSystemUserRequest.Url(), requestBody, maskinportenToken);

        // Assert
        await AssertSystemUserRequestCreated(userResponse);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUserTest_WithApp()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        // Registering system to System Register
        var testState = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        await RegisterSystem(testState, maskinportenToken);

        var requestBody = await PrepareSystemUserRequest(testState);

        // Act
        var userResponse = await
            _platformClient.PostAsync(ApiEndpoints.CreateSystemUserRequest.Url(), requestBody, maskinportenToken);

        // Assert
        await AssertSystemUserRequestCreated(userResponse);

        // Cleanup
        await _systemRegisterClient.DeleteSystem(testState.SystemId, maskinportenToken);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/576
    /// </summary>
    [Fact]
    public async Task GetRequestSystemUserStatus()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
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
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var systemInSystemRegister = await CreateSystemInSystemRegister(maskinportenToken);
        var systemUserResponse = await _systemUserClient.CreateSystemUserRequestWithExternalRef(systemInSystemRegister, maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var externalRef = Common.ExtractPropertyFromJson(systemUserResponse, "externalRef");
        _testperson = _platformClient.GetTestUserForVendor();

        // Act
        await ApproveSystemUserRequest(_testperson, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await GetSystemUserById(systemId, maskinportenToken);
        var responseByExternalRef = await _systemUserClient.GetSystemUserByExternalRef(externalRef, systemId, maskinportenToken);


        // Assert response codes
        await Common.AssertResponse(responseByExternalRef, HttpStatusCode.OK);
        await Common.AssertResponse(statusResponse, HttpStatusCode.OK);
        await Common.AssertResponse(systemUserResponseContent, HttpStatusCode.OK);

        // Assert actual content
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, await systemUserResponseContent.Content.ReadAsStringAsync());
        Assert.Contains(systemId, await responseByExternalRef.Content.ReadAsStringAsync());
    }

    private async Task<TestState> CreateSystemInSystemRegister(string maskinportenToken)
    {
        var testState = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
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
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken);
        var requestId = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var urlDelete = ApiEndpoints.DeleteSystemUserRequest.Url().Replace("{requestId}", requestId);

        // Act - Delete System User Request
        var responseDelete = await _platformClient.Delete(urlDelete, maskinportenToken);
        Assert.Equal(HttpStatusCode.Accepted, responseDelete.StatusCode);

        // Assert that System User request was deleted
        var statusResponse = await GetSystemUserRequestStatus(requestId, maskinportenToken);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
    }

    [Fact]
    public async Task ApproveRequestSystemUserTest_WithApp()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken, true);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = _platformClient.GetTestUserForVendor();

        // Act
        await ApproveSystemUserRequest(testperson, id);
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
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();

        var testState = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no")
            .WithExternalRef(externalRef)
            .WithToken(maskinportenToken);

        var systemUserResponse = await CreateSystemAndSystemUserRequest(testState, maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = _platformClient.GetTestUserForVendor();

        await ApproveSystemUserRequest(testperson, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await GetSystemUserById(systemId, maskinportenToken);
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();

        // Assert system user exists
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, content);

        // Extract system user ID
        var systemUserId = ExtractSystemUserId(content);

        // Act - Delete the system user
        await _systemUserClient.DeleteSystemUser(testperson.AltinnPartyId, systemUserId);

        // Assert - Verify system user is deleted
        var deleteVerificationResponse = await GetSystemUserById(systemId, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, deleteVerificationResponse.StatusCode);
        Assert.DoesNotContain(systemUserId!, await deleteVerificationResponse.Content.ReadAsStringAsync());

        // Verify system user request with given externalRef was also deleted
        var statusResponseAfterDelete = await GetSystemUserRequestStatus(id, maskinportenToken);
        Assert.Equal(HttpStatusCode.NotFound, statusResponseAfterDelete.StatusCode);

        //Verify that you can create a new System User Request
        var respExternalRef = await _systemUserClient.CreateSystemUserRequestWithExternalRef(testState, maskinportenToken);
        var respNoExternalRef = await _systemUserClient.CreateSystemUserRequestWithoutExternalRef(testState, maskinportenToken);
        var idNewRequestWithExternalRef = Common.ExtractPropertyFromJson(respExternalRef, "id");
        var idNewRequestWithoutExternalRef = Common.ExtractPropertyFromJson(respNoExternalRef, "id");

        await RejectSystemUserRequest(testperson.AltinnPartyId, idNewRequestWithExternalRef);
        await ApproveSystemUserRequest(testperson, idNewRequestWithoutExternalRef);

        var statusExternalRef = await GetSystemUserRequestStatus(idNewRequestWithExternalRef, maskinportenToken);
        var statusNoExternalRef = await GetSystemUserRequestStatus(idNewRequestWithoutExternalRef, maskinportenToken);
        await AssertSystemUserRequestStatus(statusExternalRef, "Rejected");
        await AssertSystemUserRequestStatus(statusNoExternalRef, "Accepted");
    }

    [Fact(Skip = "Bug reported: https://github.com/Altinn/altinn-authentication/issues/1074")]
    public async Task PutSystemUser()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var systemUserResponse = await CreateSystemAndSystemUserRequest(maskinportenToken);

        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        _testperson = _platformClient.GetTestUserForVendor();

        await ApproveSystemUserRequest(_testperson, id);
        var statusResponse = await GetSystemUserRequestStatus(id, maskinportenToken);
        var systemUserResponseContent = await GetSystemUserById(systemId, maskinportenToken);
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();

        // Assert system user exists
        await AssertSystemUserRequestStatus(statusResponse, "Accepted");
        Assert.Contains(systemId, content);

        // Extract system user ID
        _systemUserId = ExtractSystemUserId(content);

        _outputHelper.WriteLine(await systemUserResponseContent.Content.ReadAsStringAsync());

        //Put system user
        var jsonBody = $@"{{
          ""id"": ""{id}"",
          ""partyId"": ""{_testperson.AltinnPartyId}"",
          ""reporteeOrgNo"": ""{_testperson.Org}"",
          ""integrationTitle"": ""Hei, ny integration title"",
          ""systemId"": ""{systemId}""
        }}";
        
        await _systemUserClient.PutSystemUser(jsonBody, maskinportenToken);
    }

    public async Task<string> CreateSystemAndSystemUserRequest(TestState testState, string maskinportenToken)
    {
        var requestBodySystemREgister = testState.GenerateRequestBody();

        // Register system
        var response = await _systemRegisterClient.PostSystem(requestBodySystemREgister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{externalRef}", testState.ExternalRef)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        //Create system user request on the same rights that exist in the SystemRegister
        var rightsJson = JsonSerializer.Serialize(testState.Rights, Common.JsonSerializerOptions);

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Act
        var userResponse =
            await _platformClient.PostAsync("v1/systemuser/request/vendor", finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - {content}");

        return content;
    }


    public async Task<string> CreateSystemAndSystemUserRequest(string maskinportenToken, bool withApp = false)
    {
        var testState = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
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
                    new()
                    {
                        Id = "urn:altinn:resource",
                        Value = "app_ttd_endring-av-navn-v2"
                        //app_ttd_endring-av-navn-v2
                        //app_ttd_martinotest
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
        var rightsJson = JsonSerializer.Serialize(testState.Rights, Common.JsonSerializerOptions);

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Act
        var userResponse =
            await _platformClient.PostAsync(ApiEndpoints.CreateSystemUserRequest.Url(), finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - {content}");

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

    private async Task RegisterSystem(TestState testState, string maskinportenToken)
    {
        var requestBodySystemRegister = testState.GenerateRequestBody();
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
    }


    private async Task<string> PrepareSystemUserRequest(TestState testState)
    {
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl);

        var rightsJson = JsonSerializer.Serialize(testState.Rights, Common.JsonSerializerOptions);

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
        var url = ApiEndpoints.GetSystemUserRequestStatus.Url().Replace("requestId", requestId);
        return await _platformClient.GetAsync(url, token);
    }

    private static async Task AssertSystemUserRequestStatus(HttpResponseMessage response, string expectedStatus)
    {
        var responseContent = await response.Content.ReadAsStringAsync();
        var actualStatus = Common.ExtractPropertyFromJson(responseContent, "status");

        Assert.True(actualStatus != null, $"Unable to find status in response: {responseContent}");
        Assert.True(actualStatus.Equals(expectedStatus), $"Unexpected status: {actualStatus}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task ApproveSystemUserRequest(Testuser testuser, string requestId, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var approveUrl = ApiEndpoints.ApproveSystemUserRequest.Url()
            .Replace("{party}", testuser.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await ApproveRequest(approveUrl, testuser);

        Assert.True(approveResponse.StatusCode == expectedStatusCode, $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    private async Task RejectSystemUserRequest(string? altinnPartyId, string requestId)
    {
        var approveUrl = ApiEndpoints.RejectSystemUserRequest.Url()
            .Replace("{party}", altinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await ApproveRequest(approveUrl, _platformClient.GetTestUserForVendor());

        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK,
            $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    private async Task<HttpResponseMessage> GetSystemUserById(string systemId, string token)
    {
        var urlGetBySystem = ApiEndpoints.GetSystemUsersBySystemForVendor.Url().Replace("{systemId}", systemId);
        return await _platformClient.GetAsync(urlGetBySystem, token);
    }

    private static string? ExtractSystemUserId(string jsonResponse)
    {
        var jsonNode = JsonNode.Parse(jsonResponse);

        if (jsonNode is JsonObject jsonObject && jsonObject.ElementAt(1).Value is JsonArray jsonArray)
        {
            var systemUserObject = jsonArray.First()?.AsObject();
            return systemUserObject?["id"]?.GetValue<string>();
        }

        throw new Exception("Unable to extract system user ID from response.");
    }

    public void Dispose()
    {
        //Cleanup
        if (!string.IsNullOrEmpty(_systemUserId) && !string.IsNullOrEmpty(_testperson?.AltinnPartyId))
        {
            _outputHelper.WriteLine($"Cleaning up system user: {_systemUserId}");
            _systemUserClient.DeleteSystemUser(_testperson.AltinnPartyId, _systemUserId).GetAwaiter().GetResult();
        }

        GC.SuppressFinalize(this);
    }
}