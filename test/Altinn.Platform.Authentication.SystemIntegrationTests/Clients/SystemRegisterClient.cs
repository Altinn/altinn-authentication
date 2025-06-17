using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
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
    public async Task<HttpResponseMessage> PostSystem(string requestBody, string? token)
    {
        var response = await _platformClient.PostAsync(Endpoints.CreateSystemRegister.Url(), requestBody, token);

        Assert.True(response.StatusCode is HttpStatusCode.OK, $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<List<SystemResponseDto>> GetSystemsAsync(string? token)
    {
        var response = await _platformClient.GetAsync(Endpoints.GetAllSystemsFromRegister.Url(), token);

        // Assert the response status is OK
        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        // Deserialize the JSON content to a list of SystemDto
        var jsonContent = await response.Content.ReadAsStringAsync();
        var systems = JsonSerializer.Deserialize<List<SystemResponseDto>>(jsonContent, Common.JsonSerializerOptions);
        return systems ?? [];
    }

    public async Task DeleteSystem(string systemId, string? token)
    {
        var resp = await _platformClient.Delete($"{Endpoints.DeleteSystemSystemRegister.Url()}".Replace("{systemId}", systemId), token);
        Assert.True(HttpStatusCode.OK == resp.StatusCode, $"{resp.StatusCode}  {await resp.Content.ReadAsStringAsync()}");
    }

    public async Task UpdateRightsOnSystem(string systemId, string requestBody, string? token)
    {
        var putUrl = Endpoints.UpdateRightsVendorSystemRegister.Url().Replace("{systemId}", systemId);
        var putResponse = await _platformClient.PutAsync(putUrl, requestBody, token);

        await Common.AssertResponse(putResponse, HttpStatusCode.OK);
    }

    public async Task<HttpResponseMessage> getBySystemId(string systemId, string? token)
    {
        var getUrl = Endpoints.GetVendorSystemRegisterById.Url().Replace("{systemId}", systemId);
        var getResponse = await _platformClient.GetAsync(getUrl, token);
        return getResponse;
    }

    public TestState CreateStandardSystemWithAccessPackage(string? token)
    {
        return new TestState("Resources/Testdata/Systemregister/AccessPackageSystemRegister.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithName(Guid.NewGuid().ToString())
            .WithToken(token);
    }
}