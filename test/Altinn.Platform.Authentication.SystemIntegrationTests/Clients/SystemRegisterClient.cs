using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
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
    public async Task<HttpResponseMessage> PostSystem(SystemRegisterState state)
    {
        // Prepare
        var requestBody =
            await SystemRegisterTests.GetRequestBodyWithReplacements(state,
                "Resources/Testdata/Systemregister/CreateNewSystem.json");

        Assert.True(state.Token != null, "Token should not be empty");
        
        

        var response = await _platformClient.PostAsync(
            "authentication/api/v1/systemregister/vendor", requestBody, state.Token);

        Assert.True(HttpStatusCode.OK == response.StatusCode,
            $"{response.StatusCode}  {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<SystemRegisterState> CreateSystemRegisterUser()
    {
        // Prerequisite-step
        var maskinportenToken = await _platformClient.GetToken();

        var teststate = new SystemRegisterState()
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        await PostSystem(teststate);

        return teststate;
    }
}