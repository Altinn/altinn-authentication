using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationTests
{
    public const string accessPackageName = "urn:altinn:accesspackage:skattnaering";
    private readonly ITestOutputHelper _outputHelper;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemRegisterClient _systemRegisterClient;

    /// <summary>
    /// Systemregister tests
    /// </summary>
    /// <param name="outputHelper">For test logging purposes</param>
    public ClientDelegationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    [Fact]
    public async Task SystemRegisterWithAccessPackageTest()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: accessPackageName, id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        await _systemRegisterClient.PostSystem(teststate.GenerateRequestBody(), maskinportenToken);

        const string jsonBody = @"[
                      {
                        ""action"": ""read"",
                        ""resource"": [
                          { 
                            ""id"": ""urn:altinn:resource"",
                            ""value"": ""authentication-e2e-test""
                          }
                        ]
                      },
                      {
                        ""action"": ""read"",
                        ""resource"": [
                          {
                            ""id"": ""urn:altinn:resource"",
                            ""value"": ""vegardtestressurs""
                          }
                        ]
                      }
                    ]";
        await _systemRegisterClient.UpdateRightsOnSystem(teststate.SystemId, jsonBody, maskinportenToken);

        var getForVendor =
            await _platformClient.GetAsync($"{ApiEndpoints.GetVendorSystemRegisterById.Url()}".Replace("{systemId}", teststate.SystemId), maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, getForVendor.StatusCode);

        var stringBody = await getForVendor.Content.ReadAsStringAsync();

        Assert.Contains("authentication-e2e-test", stringBody);
        Assert.Contains("vegardtestressurs", stringBody);
        Assert.DoesNotContain("resource_nonDelegable_enkeltrettighet", stringBody);
    }
}