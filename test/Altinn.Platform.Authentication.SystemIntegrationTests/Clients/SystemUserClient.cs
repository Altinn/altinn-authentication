using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.VendorClientDelegation;
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
        var urlGetBySystem = Endpoints.GetSystemUsersByPartyAgent.Url().Replace("{party}", party);
        var response = await _platformClient.GetAsync(urlGetBySystem, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<List<SystemUser>> GetSystemUsersForAgentTestUser(Testuser testuser)
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
        var urlGetBySystem = Endpoints.GetSystemUsersBySystemForVendor.Url().Replace("{systemId}", systemId);
        return await _platformClient.GetAsync(urlGetBySystem, token);
    }


    public async Task<HttpResponseMessage> GetSystemUserByExternalRef(string externalRef, string systemId, string? maskinportenToken)
    {
        var urlGetBySystem =
            Endpoints.GetSystemUserRequestByExternalRef.Url()
                .Replace("{externalRef}", externalRef)
                .Replace("{systemId}", systemId)
                .Replace("{orgNo}", _platformClient.EnvironmentHelper.Vendor);

        return await _platformClient.GetAsync(urlGetBySystem, maskinportenToken);
    }

    public async Task<string> GetSystemUserVendorByQuery(string systemId, string? orgNo, string externalRef, string? maskinportenToken)
    {
        var url = Endpoints.GetSystemUserVendorByQuery.Url()
            .Replace("{systemId}", systemId)
            .Replace("{orgNo}", orgNo)
            .Replace("{externalRef}", externalRef);

        var response = await _platformClient.GetAsync(url, maskinportenToken);

        // Assert 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Extract just the "id" field
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var id = doc.RootElement.GetProperty("id").GetString();

        Assert.False(string.IsNullOrWhiteSpace(id), "Response JSON did not contain a valid 'id'");

        return id;
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

    public async Task<HttpResponseMessage> ApproveRequest(string? endpoint, Testuser? testperson)
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

    public async Task<ClientsForDelegationResponseDto> GetAvailableClientsForVendor(Testuser? facilitator, string? systemUserId, bool requireNonEmpty = true)
    {
        var url = Endpoints.VendorGetAvailableClients.Url() + $"?agent={systemUserId}";

        var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.read");

        // Call and assert HTTP 200
        var response = await _platformClient.GetAsync(url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize
        var clients = JsonSerializer.Deserialize<ClientsForDelegationResponseDto>(await response.Content.ReadAsStringAsync());

        Assert.NotNull(clients);
        if (requireNonEmpty)
        {
            Assert.NotNull(clients.Data);
            Assert.True(clients.Data.Count > 0, "No clients found for vendor");
        }

        return clients;
    }

    public async Task<HttpResponseMessage> AddClient(Testuser? facilitator, string? systemUserId, string clientId)
    {
        var urlPost = Endpoints.VendorAddClients.Url().Replace("{clientId}", clientId).Replace("{systemUserId}", systemUserId);

        var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.write");
        return await _platformClient.PostAsyncWithNoBody(urlPost, token);
    }

    public async Task DelegateAllClientsFromVendorToSystemUser(Testuser facilitator, string? systemUserId, List<ClientInfoDto> customersData)
    {
        foreach (ClientInfoDto clientInfoDto in customersData)
        {
            HttpResponseMessage resp = await AddClient(facilitator, systemUserId, clientInfoDto.ClientId.ToString());
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"Failed to add client: Unexpected status code: {resp.StatusCode}");
        }
    }

    public async Task DeleteAllClientsFromVendorSystemUser(Testuser facilitator, string systemUserId, List<ClientInfoDto> customersData)
    {
        foreach (ClientInfoDto clientInfoDto in customersData)
        {
            HttpResponseMessage resp = await DeleteClient(facilitator, systemUserId, clientInfoDto.ClientId.ToString());
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"Unexpected status code: {resp.StatusCode}");
        }
    }

    private async Task<HttpResponseMessage> DeleteClient(Testuser facilitator, string? systemUserId, string clientId)
        {
            var urlDelete = Endpoints.VendorDeleteClient.Url()
                .Replace("{clientId}", clientId)
                .Replace("{systemUserId}", systemUserId);

            var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.write altinn:clientdelegations.read");
            return await _platformClient.Delete(urlDelete, token);
        }

        public async Task<ClientsForDelegationResponseDto> GetDelegatedClientsFromVendorSystemUser(Testuser? facilitator, string? systemUserId)
        {
            var urlGet = Endpoints.VendorGetDelegatedClients.Url().Replace("{systemUserId}", systemUserId);

            var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.read");
            var response = await _platformClient.GetAsync(urlGet, token);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Unexpected status code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();

            // ClientsForDelegationResponseDto
            ClientsForDelegationResponseDto resp = JsonSerializer.Deserialize<ClientsForDelegationResponseDto>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Unable to deserialize ");
            return resp;
        }

        public async Task<List<SystemUserAgentDto>> GetSystemUserAgents(Testuser? facilitator)
        {
            var urlGet = Endpoints.VendorGetSystemUserAgents.Url() + $"?party={facilitator.Org}";

            var token = await _platformClient.GetPersonalAltinnToken(facilitator, "altinn:clientdelegations.read");
            var response = await _platformClient.GetAsync(urlGet, token);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Unexpected status code: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            List<SystemUserAgentDto>? agents = JsonSerializer.Deserialize<List<SystemUserAgentDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return agents ?? [];
        }

        public async Task<HttpResponseMessage> DeleteAgentSystemUser(string? systemUserId, Testuser? facilitator)
        {
            var url = Endpoints.DeleteAgentSystemUser.Url()
                ?.Replace("{party}", facilitator.AltinnPartyId)
                .Replace("{systemUserId}", systemUserId);

            url += $"?facilitatorId={facilitator.AltinnPartyUuid}";

            return await _platformClient.Delete(url, facilitator.AltinnToken);
        }
    }