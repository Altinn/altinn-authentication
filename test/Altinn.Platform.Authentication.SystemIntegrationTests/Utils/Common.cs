using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Xunit;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class Common
{
    private readonly PlatformAuthenticationClient _platformClient;

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public Common(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
    }

    private SystemRegisterClient SystemRegisterClient => _platformClient.SystemRegisterClient;
    private SystemUserClient SystemUserClient => _platformClient.SystemUserClient;

    public async Task<string> CreateAndApproveSystemUserRequest(string? maskinportenToken, string externalRef,
        Testuser testuser, string clientId)
    {
        var testState = new TestState("Resources/Testdata/ChangeRequest/CreateNewSystem.json")
            .WithClientId(clientId)
            .WithVendor(testuser.Org)
            .WithRedirectUrl("https://altinn.no")
            .WithName("Change Request-test: " + Guid.NewGuid())
            .WithToken(maskinportenToken);

        var requestBodySystemREgister = testState.GenerateRequestBody();

        // Register system
        var response = await SystemRegisterClient.PostSystem(requestBodySystemREgister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ChangeRequest/CreateSystemUserRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl)
            .Replace("{externalRef}", externalRef);

        // Act
        var userResponse = await _platformClient.PostAsync(Endpoints.CreateSystemUserRequest.Url(), requestBody,
            maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();

        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        using var jsonDocSystemRequestResponse = JsonDocument.Parse(content);
        var id = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();


        // Approve
        var approveResp =
            await ApproveRequest($"authentication/api/v1/systemuser/request/{testuser.AltinnPartyId}/{id}/approve",
                testuser);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");

        return testState.SystemId;
    }

    public async Task<HttpContent> GetSystemUserForVendor(string systemId, string? maskinportenToken)
    {
        var endpoint = $"authentication/api/v1/systemuser/vendor/bysystem/{systemId}";
        var resp = await _platformClient.GetAsync(endpoint, maskinportenToken);
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            "Did not get OK, but: " + resp.StatusCode + " for endpoint:  " + endpoint);
        return resp.Content;
    }

    public async Task<HttpContent> GetSystemUserForVendorAgent(string systemId, string? maskinportenToken)
    {
        var url = Endpoints.GetVendorAgentRequestsBySystemId.Url()?.Replace("{systemId}", systemId);
        var resp = await _platformClient.GetAsync(url, maskinportenToken);
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            "Did not get OK, but: " + resp.StatusCode + " for endpoint:  " + url);
        return resp.Content;
    }

    public async Task<HttpResponseMessage> ApproveRequest(string? endpoint, Testuser testperson)
    {
        // Use the PostAsync method for the approval request
        var response = await _platformClient.PostAsync(endpoint, string.Empty, testperson.AltinnToken);
        return response;
    }

    public static void AssertSuccess(HttpResponseMessage response, string message)
    {
        Assert.True(response.IsSuccessStatusCode,
            $"{message}. Received: {response.StatusCode} and response text was: {response.Content.ReadAsStringAsync().Result}");
    }

    public static string ExtractPropertyFromJson(string json, string propertyName)
    {
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // If the property is directly at the root level
        if (root.TryGetProperty(propertyName, out var directProperty))
        {
            return directProperty.GetString()
                   ?? throw new Exception($"Property '{propertyName}' is null in JSON: {json}");
        }

        // If the property is inside an array under "data"
        if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty(propertyName, out var arrayProperty))
                {
                    return arrayProperty.GetString()
                           ?? throw new Exception($"Property '{propertyName}' is null in JSON array: {json}");
                }
            }
        }

        throw new Exception($"Property '{propertyName}' not found in JSON: {json}");
    }

    public static async Task AssertResponse(HttpResponseMessage response, HttpStatusCode statusCode)
    {
        Assert.True(statusCode == response.StatusCode,
            $"[Response was {response.StatusCode} : Response body was: {await response.Content.ReadAsStringAsync()}]");
    }

    public async Task CreateRequestWithManalExample(string? maskinportenToken, string externalRef, Testuser testuser,
        string clientId)
    {
        var testState = new TestState("Resources/Testdata/Systemregister/VendorExampleUrls.json")
            .WithName("E2E tests - Redirect URL" + Guid.NewGuid())
            .WithClientId(clientId)
            .WithVendor(testuser.Org)
            .WithAllowedRedirectUrls(
                "https://www.cloud-booking.net/misc/integration.htm?integration=Altinn3&action=authCallback",
                "https://test.cloud-booking.net/misc/integration.htm?integration=Altinn3&action=authCallback"
            )
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = testState.GenerateRequestBody();

        // Register system
        var response = await SystemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ChangeRequest/CreateSystemUserRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", "")
            .Replace("{externalRef}", externalRef);

        // Act
        var userResponse = await _platformClient.PostAsync(Endpoints.CreateSystemUserRequest.Url(), requestBody,
            maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();

        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        using var jsonDocSystemRequestResponse = JsonDocument.Parse(content);
        var id = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();

        var url = Endpoints.ApproveSystemUserRequest.Url()
            ?.Replace("{party}", testuser.AltinnPartyId)
            .Replace("{requestId}", id);
        // Approve
        var approveResp =
            await ApproveRequest(url, testuser);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");
    }

    public async Task<SystemUser?> GetSystemUserOnSystemIdForAgenOnOrg(string systemId, Testuser testuser,
        string externalRef = "")
    {
        var systemUsers = await SystemUserClient.GetSystemUsersForAgentTestUser(testuser);
        return systemUsers.Find(user => user.SystemId == systemId && user.ExternalRef == externalRef);
    }

    public async Task GetTokenForSystemUser(string? clientId, string? systemUserOwnerOrgNo, string? externalRef)
    {
        const string scopes = "altinn:maskinporten/systemuser.read";
        var systemProviderOrgNo = _platformClient.EnvironmentHelper.Vendor;

        var altinnEnterpriseToken =
            await _platformClient.GetEnterpriseAltinnToken(systemProviderOrgNo, scopes);

        var queryString =
            $"?clientId={clientId}" +
            $"&systemProviderOrgNo={systemProviderOrgNo}" +
            $"&systemUserOwnerOrgNo={systemUserOwnerOrgNo}" +
            $"&externalRef={externalRef}";

        var fullEndpoint = $"{Endpoints.GetSystemUserByExternalId.Url()}{queryString}";

        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        Assert.NotNull(resp);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    public async Task DeleteSystem(string systemId, string? token)
    {
        var resp = await _platformClient.Delete(
            $"{Endpoints.DeleteSystemSystemRegister.Url()}".Replace("{systemId}", systemId), token);
        Assert.True(HttpStatusCode.OK == resp.StatusCode,
            $"{resp.StatusCode}  {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Creates a new system in Systemregister. Requires Bearer token from Maskinporten
    /// </summary>
    public async Task<HttpResponseMessage> PostSystem(string requestBody, string? token)
    {
        var response = await _platformClient.PostAsync(Endpoints.CreateSystemRegister.Url(), requestBody, token);
        Assert.True(response.StatusCode is HttpStatusCode.OK,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");
        return response;
    }
}