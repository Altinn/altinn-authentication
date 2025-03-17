using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class Common
{
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;
    public readonly ITestOutputHelper Output;
    public static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public Common(PlatformAuthenticationClient platformClient, ITestOutputHelper output)
    {
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(platformClient);
        Output = output;
    }

    public async Task<string> CreateAndApproveSystemUserRequest(string maskinportenToken, string externalRef, Testuser testuser, string clientId)
    {
        var testState = new TestState("Resources/Testdata/ChangeRequest/CreateNewSystem.json")
            .WithClientId(clientId)
            .WithVendor(testuser.Org)
            .WithRedirectUrl("https://altinn.no")
            .WithToken(maskinportenToken);

        var requestBodySystemREgister = testState.GenerateRequestBody();

        // Register system
        var response = await _systemRegisterClient.PostSystem(requestBodySystemREgister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ChangeRequest/CreateSystemUserRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl)
            .Replace("{externalRef}", externalRef);

        // Act
        var userResponse = await _platformClient.PostAsync("v1/systemuser/request/vendor", requestBody, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();

        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        using var jsonDocSystemRequestResponse = JsonDocument.Parse(content);
        var id = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();

        // Approve
        var approveResp =
            await ApproveRequest($"v1/systemuser/request/{testuser.AltinnPartyId}/{id}/approve", testuser);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");

        return testState.SystemId;
    }

    public async Task<HttpContent> GetSystemUserForVendor(string systemId, string maskinportenToken)
    {
        var endpoint = $"v1/systemuser/vendor/bysystem/{systemId}";
        var resp = await _platformClient.GetAsync(endpoint, maskinportenToken);
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            "Did not get OK, but: " + resp.StatusCode + " for endpoint:  " + endpoint);
        return resp.Content;
    }

    public async Task<HttpResponseMessage> ApproveRequest(string endpoint, Testuser testperson)
    {
        // Get the Altinn token
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await _platformClient.PostAsync(endpoint, string.Empty, altinnToken);
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
        return jsonDoc.RootElement.GetProperty(propertyName).GetString()
               ?? throw new Exception($"Property '{propertyName}' not found in JSON: {json}");
    }

    public static async Task AssertResponse(HttpResponseMessage response, HttpStatusCode statusCode)
    {
        Assert.True(statusCode == response.StatusCode, $"[Response was {response.StatusCode} : Response body was: {await response.Content.ReadAsStringAsync()}]");
    }

    public async Task<object> CreateRequestWithManalExample(string maskinportenToken, string externalRef, Testuser testuser, string clientId)
    {
        var testState = new TestState("Resources/Testdata/Systemregister/VendorExampleUrls.json")
            .WithClientId(clientId)
            .WithVendor(testuser.Org)
            .WithAllowedRedirectUrls(
                "https://www.cloud-booking.net/misc/integration.htm?integration=Altinn3&action=authCallback",
                "https://test.cloud-booking.net/misc/integration.htm?integration=Altinn3&action=authCallback"
            )
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = testState.GenerateRequestBody();
        Output.WriteLine("request body from Register: " + requestBodySystemRegister);

        // Register system
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ChangeRequest/CreateSystemUserRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            // .Replace("{redirectUrl}", testState.AllowedRedirectUrls.First())
            .Replace("{redirectUrl}",testState.AllowedRedirectUrls.First() + "&clientId=123")
            .Replace("{externalRef}", externalRef);
        
        Output.WriteLine("Request body for system user request" + requestBody);

        // Act
        var userResponse = await _platformClient.PostAsync("v1/systemuser/request/vendor", requestBody, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();


        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        using var jsonDocSystemRequestResponse = JsonDocument.Parse(content);
        var id = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();

        // Approve
        var approveResp =
            await ApproveRequest($"v1/systemuser/request/{testuser.AltinnPartyId}/{id}/approve", testuser);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");

        return testState.SystemId;
    }
}