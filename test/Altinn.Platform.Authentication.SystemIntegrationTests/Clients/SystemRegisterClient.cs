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
public class SystemRegisterClient
{
    private readonly PlatformAuthenticationClient _platformClient;

    public SystemRegisterClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
    }

    /// <summary>
    /// Creates a new system in Systemregister. Requires Bearer token from Maskinporten
    /// </summary>
    public async Task<HttpResponseMessage> PostSystem(string requestBody, string token)
    {
        var response = await _platformClient.PostAsync(ApiEndpoints.CreateSystemRegister.Url(), requestBody, token);

        Assert.True(response.StatusCode is HttpStatusCode.OK, $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<List<SystemDto>> GetSystemsAsync(string token)
    {
        var response = await _platformClient.GetAsync(ApiEndpoints.GetAllSystemsFromRegister.Url(), token);

        // Assert the response status is OK
        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        // Deserialize the JSON content to a list of SystemDto
        var jsonContent = await response.Content.ReadAsStringAsync();
        var systems = JsonSerializer.Deserialize<List<SystemDto>>(jsonContent, Common.JsonSerializerOptions);
        return systems ?? [];
    }

    public async Task DeleteSystem(string SystemId, string token)
    {
        var resp = await _platformClient.Delete($"{ApiEndpoints.DeleteSystemSystemRegister.Url()}".Replace("{systemId}", SystemId), token);
        Assert.True(HttpStatusCode.OK == resp.StatusCode, $"{resp.StatusCode}  {await resp.Content.ReadAsStringAsync()}");
    }

    public async Task<HttpResponseMessage> UpdateRightsOnSystem(string systemId, string requestBody, string? token)
    {
        var putUrl = ApiEndpoints.UpdateRightsVendorSystemRegister.Url().Replace("{systemId}", systemId);
        var putResponse = await _platformClient.PutAsync(putUrl, requestBody, token);

        await Common.AssertResponse(putResponse, HttpStatusCode.OK);
        return putResponse;
    }

    public async Task<HttpResponseMessage> getBySystemId(string systemId, string token)
    {
        var getUrl = ApiEndpoints.GetVendorSystemRegisterById.Url().Replace("{systemId}", systemId);
        var getResponse = await _platformClient.GetAsync(getUrl, token);
        return getResponse;
    }
}