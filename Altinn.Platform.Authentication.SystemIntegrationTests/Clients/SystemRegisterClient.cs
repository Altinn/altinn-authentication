using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

/// <summary>
/// For specific requests needed for System Register tests or test data generation purposes
/// </summary>
public class SystemRegisterClient : PlatformAuthenticationClient
{
    private Helper Helper { get; set; }
    private ITestOutputHelper Output { get; set; }

    /// <summary>
    /// For specific Sytem Register Requests
    /// </summary>
    public SystemRegisterClient(ITestOutputHelper output)
    {
        Output = output;
        Helper = new Helper(Output);
    }

    /// <summary>
    /// Creates a new system in Systmeregister. Requires Bearer token from Maskinporten
    /// </summary>
    public async Task<HttpResponseMessage> PostSystem(SystemRegisterState state)
    {
        // Prepare
        var requestBody =
            await SystemRegisterTests.GetRequestBodyWithReplacements(state,
                "Resources/Testdata/Systemregister/CreateNewSystem.json");

        var response = await PostAsync("authentication/api/v1/systemregister/vendor", requestBody, state.Token);

        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return response;
    }
}