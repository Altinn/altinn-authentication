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
        var response = await _platformClient.PostAsync(UrlConstants.PostSystemRegister, requestBody, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode, $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<List<SystemDto>> GetSystemsAsync(string token)
    {
        var response = await _platformClient.GetAsync(UrlConstants.GetSystemRegister, token);

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
        var resp = await _platformClient.Delete($"{UrlConstants.DeleteSystemRegister}/{SystemId}", token);
        Assert.True(HttpStatusCode.OK == resp.StatusCode, $"{resp.StatusCode}  {await resp.Content.ReadAsStringAsync()}");
    }
}