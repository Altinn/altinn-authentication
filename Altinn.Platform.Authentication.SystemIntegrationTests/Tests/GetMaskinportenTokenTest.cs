using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
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
    /// Test that we're able to read from jwks folder
    /// </summary>
    [Fact]
    public async Task ReadJwkFile()
    {
        var jsonString = await Helper.ReadFile("Resources/Jwks/unitTestJwks.json");

        var jwk =
            JsonSerializer.Deserialize<Jwk>(jsonString);

        Assert.Equal("RSA", jwk?.kty);
        Assert.Equal("samplevaluep", jwk?.p);
        Assert.Equal("samplevalueq", jwk?.q);
        Assert.Equal("d", jwk?.d);
        Assert.Equal("AQAB", jwk?.e);
        Assert.Equal("samplevaluesig", jwk?.use);
        Assert.Equal("authentication-systemintegration-tests-TEST.2024-10-23", jwk?.kid);
        Assert.Equal("samplevalueqi", jwk?.qi);
        Assert.Equal("samplevaluedp", jwk?.dp);
        Assert.Equal("RS256", jwk?.alg);
        Assert.Equal("samplevaluedq", jwk?.dq);
        Assert.Equal("samplevaluen", jwk?.n);
    }

    /// <summary>
    /// Make sure we test that the ExchangeToken endpoint is up and running
    /// </summary>
    [Fact]
    public async Task GetExchangeToken()
    {
        var maskinportenToken = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");
        var altinnToken = await _platformAuthenticationClient.GetExchangeToken(maskinportenToken.Token);
        Assert.NotEmpty(altinnToken);
    }

    [Fact]
    public async Task GetBearerToken()
    {
        var maskinportenToken = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");
        Assert.NotNull(maskinportenToken);
        Assert.NotEmpty(maskinportenToken.Token);
        Assert.NotNull(maskinportenToken.ClientId);
    }
}