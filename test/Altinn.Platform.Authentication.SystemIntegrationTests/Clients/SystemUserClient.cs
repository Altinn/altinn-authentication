using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

/// <summary>
/// For specific requests needed for System Register tests or test data generation purposes
/// </summary>
public class SystemUserClient
{
    private readonly PlatformAuthenticationClient _platformClient;

    public SystemUserClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
    }

    public async Task<HttpResponseMessage> GetSystemuserForParty(string? party, string? token)
    {
        var urlGetBySystem = ApiEndpoints.GetSystemUsersByParty.Url()
            .Replace("{party}", party);
        var response = await _platformClient.GetAsync(urlGetBySystem, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }
 

    public async Task<HttpResponseMessage> GetSystemuserForPartyAgent(string? party, string? token)
    {
        var urlGetBySystem = ApiEndpoints.GetSystemUsersByPartyAgent.Url()
            .Replace("{party}", party);
        var response = await _platformClient.GetAsync(urlGetBySystem, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<List<SystemUser>> GetSystemUsersForAgentTestUser(Testuser testuser, String externalRef = "")
    {
        var resp = await GetSystemuserForPartyAgent(testuser.AltinnPartyId, testuser.AltinnToken);

        var content = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SystemUser>>(content, Common.JsonSerializerOptions) ?? [];
    }
    
    public async Task<List<SystemUser>> GetSystemUsersForTestUser(Testuser testuser)
    {
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testuser);
        var resp = await GetSystemuserForParty(testuser.AltinnPartyId, altinnToken);

        var content = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SystemUser>>(content, Common.JsonSerializerOptions) ?? [];
    }


    public async Task<HttpResponseMessage> GetSystemUserByExternalRef(string externalRef, string systemId, string? maskinportenToken)
    {
        var urlGetBySystem =
            ApiEndpoints.GetSystemUserRequestByExternalRef.Url()
                .Replace("{externalRef}", externalRef)
                .Replace("{systemId}", systemId)
                .Replace("{orgNo}", _platformClient.EnvironmentHelper.Vendor);

        return await _platformClient.GetAsync(urlGetBySystem, maskinportenToken);
    }

    public async Task<string> CreateSystemUserRequestWithoutExternalRef(TestState testState, string? maskinportenToken)
    {
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

    public async Task<string> CreateSystemUserRequestWithExternalRef(TestState testState, string? maskinportenToken)
    {
        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequestExternalRef.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl)
            .Replace("{externalRef}", testState.ExternalRef);

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

    public async Task ApproveSystemUserRequest(Testuser testuser, string requestId)
    {
        var approveUrl = ApiEndpoints.ApproveSystemUserRequest.Url()
            .Replace("{partyId}", testuser.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await ApproveRequest(approveUrl, testuser);

        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK,
            $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    public async Task<HttpResponseMessage> ApproveRequest(string endpoint, Testuser testperson)
    {
        // Get the Altinn token
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await _platformClient.PostAsync(endpoint, string.Empty, altinnToken);
        return response;
    }

    public async Task DeleteSystemUser(string? altinnPartyId, string? systemUserId)
    {
        var deleteUrl = ApiEndpoints.DeleteSystemUserById.Url()
            .Replace("{party}", altinnPartyId)
            .Replace("{systemUserId}", systemUserId);
        var deleteResponse = await _platformClient.DeleteRequest(deleteUrl, _platformClient.GetTestUserForVendor());

        Assert.Equal(HttpStatusCode.Accepted, deleteResponse.StatusCode);
    }

    public async Task PutSystemUser(string requestBody, string? token)
    {
        var putUrl = ApiEndpoints.UpdateSystemUser.Url();
        var putResponse = await _platformClient.PutAsync(putUrl, requestBody, token);

        Assert.Equal(HttpStatusCode.Accepted, putResponse.StatusCode);
    }
}