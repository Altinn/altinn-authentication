using System.Net;
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
        var response = await _platformClient.PostAsync(
            "v1/systemregister/vendor", requestBody, token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }
}