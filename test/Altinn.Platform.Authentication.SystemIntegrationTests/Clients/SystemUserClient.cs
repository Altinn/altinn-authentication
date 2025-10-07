using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.VendorClientDelegation;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
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
        var urlGetBySystem = Endpoints.GetSystemUsersByParty.Url()
            .Replace("{party}", party);
        var response = await _platformClient.GetAsync(urlGetBySystem, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }


    public async Task<HttpResponseMessage> GetSystemuserForPartyAgent(string? party, string? token)
    {
        var urlGetBySystem = Endpoints.GetSystemUsersByPartyAgent.Url()
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

    public async Task<HttpResponseMessage> GetSystemUserById(string systemId, string? token)
    {
        var urlGetBySystem = Endpoints.GetSystemUsersBySystemForVendor.Url()?.Replace("{systemId}", systemId);
        return await _platformClient.GetAsync(urlGetBySystem, token);
    }


    public async Task<HttpResponseMessage> GetSystemUserByExternalRef(string externalRef, string systemId, string? maskinportenToken)
    {
        var urlGetBySystem =
            Endpoints.GetSystemUserRequestByExternalRef.Url()
                ?.Replace("{externalRef}", externalRef)
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
            await _platformClient.PostAsync(Endpoints.CreateSystemUserRequest.Url(), finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - {content}");

        return content;
    }

    public async Task<string> CreateSystemUserRequestWithExternalRef(TestState testState, string? maskinportenToken, string externalRef)
    {
        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequestExternalRef.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{redirectUrl}", testState.RedirectUrl)
            .Replace("{externalRef}", externalRef);

        //Create system user request on the same rights that exist in the SystemRegister
        var rightsJson = JsonSerializer.Serialize(testState.Rights, Common.JsonSerializerOptions);

        var finalJson = requestBody.Replace("{rights}", $"\"rights\": {rightsJson},");

        // Act
        var userResponse =
            await _platformClient.PostAsync(Endpoints.CreateSystemUserRequest.Url(), finalJson, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - {content}");

        return content;
    }

    public async Task ApproveSystemUserRequest(Testuser testuser, string requestId)
    {
        var approveUrl = Endpoints.ApproveSystemUserRequest.Url()
            ?.Replace("{partyId}", testuser.AltinnPartyId)
            .Replace("{party}", testuser.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await ApproveRequest(approveUrl, testuser);

        Assert.True(approveResponse.StatusCode == HttpStatusCode.OK,
            $"Approval failed with status code: {approveResponse.StatusCode}");
    }

    public async Task<HttpResponseMessage> ApproveRequest(string? endpoint, Testuser testperson)
    {
        // Get the Altinn token
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await _platformClient.PostAsync(endpoint, string.Empty, altinnToken);
        return response;
    }

    public async Task DeleteSystemUser(string? altinnPartyId, string? systemUserId)
    {
        var deleteUrl = Endpoints.DeleteSystemUserById.Url()
            .Replace("{party}", altinnPartyId)
            .Replace("{systemUserId}", systemUserId);
        var deleteResponse = await _platformClient.DeleteRequest(deleteUrl, _platformClient.GetTestUserForVendor());

        Assert.Equal(HttpStatusCode.Accepted, deleteResponse.StatusCode);
    }

    public async Task PutSystemUser(string requestBody, string? token)
    {
        var putUrl = Endpoints.UpdateSystemUser.Url();
        var putResponse = await _platformClient.PutAsync(putUrl, requestBody, token);

        Assert.Equal(HttpStatusCode.Accepted, putResponse.StatusCode);
    }

    public async Task<ClientsForDelegationResponseDto?> GetAvailableClientsForVendor(Testuser facilitator, string? systemUserId)
    {
        var urlGetBySystem = Endpoints.VendorGetAvailableClients.Url() + $"?agent={systemUserId}";

        var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.read");
        return await _platformClient.GetAsyncOnType<ClientsForDelegationResponseDto>(urlGetBySystem, token);
    }

    public async Task<HttpResponseMessage> AddClient(Testuser facilitator, string? systemUserId, string clientId)
    {
        var urlPost = Endpoints.VendorAddClients.Url()?.Replace("{clientId}", clientId).Replace("{systemUserId}", systemUserId);

        var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.write");
        return await _platformClient.PostAsyncWithNoBody(urlPost, token);
    }

    public Task DelegateAllClientsFromVendorToSystemUser(Testuser facilitator, string? systemUserId, List<ClientInfoDto> customersData)
    {
        foreach (ClientInfoDto clientInfoDto in customersData)
        {
            var resp = AddClient(facilitator, systemUserId, clientInfoDto.ClientId.ToString());
            Assert.True(resp.Result.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {resp.Result.StatusCode}");
        }

        return Task.CompletedTask;
    }
}