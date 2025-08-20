using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Test that we're able to get a Machineporten token
/// </summary>
[Trait("Category", "IntegrationTest")]
public class GetMaskinportenTokenTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly PlatformAuthenticationClient _platformAuthenticationClient;

    /// <summary>
    /// Test machine porten functionality
    /// </summary>
    public GetMaskinportenTokenTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _platformAuthenticationClient = new PlatformAuthenticationClient();
    }

    /// <summary>
    /// Make sure we test that the ExchangeToken endpoint is up and running
    /// </summary>
    [Fact]
    public async Task GetExchangeToken()
    {
        var maskinportenToken = await _platformAuthenticationClient.GetMaskinportenTokenForVendor();
        var altinnToken = await _platformAuthenticationClient.GetExchangeToken(maskinportenToken);
        Assert.NotEmpty(altinnToken);
    }

    [Fact]
    public async Task GetBearerToken()
    {
        var maskinportenToken = await _platformAuthenticationClient.GetMaskinportenTokenForVendor();
        Assert.NotNull(maskinportenToken);
    }
    
    [Fact]
    public async Task GetConsentToken()
    {
        var pid = "04855195742";
        var from = $"urn:altinn:person:identifier-no:{pid}";
        
        var maskinportenToken = await _platformAuthenticationClient.GetConsentToken("1542da16-dda8-47ff-8eda-16954d1d1a62", from);
        Assert.NotNull(maskinportenToken);
    }
}