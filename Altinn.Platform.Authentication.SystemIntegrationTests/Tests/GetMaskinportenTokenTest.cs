using System.Text.Json;
using Altinn.AccessManagement.SystemIntegrationTests.Domain;
using Altinn.AccessManagement.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Test that we're able to get a Machineporten token
/// </summary>
public class GetMaskinportenTokenTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly MaskinPortenTokenGenerator _maskinPortenTokenGenerator = new();
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
        _testOutputHelper.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");

        var test =
            JsonSerializer.Deserialize<Jwk>(jsonString);

        Assert.Equal("RSA", test?.kty);
        Assert.Equal("_Pasldkalsødkasd", test?.p);
        Assert.Equal("ALKSdløaskdløasd", test?.q);
        Assert.Equal("h5u_77Q", test?.d);
        Assert.Equal("AQAB", test?.e);
        Assert.Equal("sig", test?.use);
        Assert.Equal("SystembrukerForSpesifikkOrgVegard", test?.kid);
        Assert.Equal("NxdzozNmkgIWIUFoRldlT1mVdE_H-8aJHdl4pUgI1J4iZanGhPgwGiOiFrHb3YLFQL0", test?.qi);
        Assert.Equal("2BJcrDuPJSL4kmi8epxNhRP-I0Kx78FwQWZ8", test?.dp);
        Assert.Equal("RS256", test?.alg);
        Assert.Equal("b_CE3QmIMsksEIVF178Ah2MqbJbPk", test?.dq);
        Assert.Equal("NzYiQSN_RNk-LSCqoMjPXCUv7g-Q", test?.n);
    }

    /// <summary>
    /// Test that we're able to get a Machineporten token on behalf of an organization
    /// </summary>
    [Fact]
    public async Task GetTokenAsOrganization()
    {
        var token = await _maskinPortenTokenGenerator.GenerateJwt();
        Assert.NotEmpty(token);

        var maskinportenToken = await _maskinPortenTokenGenerator.RequestToken(token);
        Assert.Contains("systemregister.write", maskinportenToken);
        Assert.Contains("access_token", maskinportenToken);
    }

    /// <summary>
    /// Make sure we test that the ExchangeToken endpoint is up and running
    /// </summary>
    [Fact]
    public async Task GetExchangeToken()
    {
        var token = await _maskinPortenTokenGenerator.GenerateJwt();
        Assert.NotEmpty(token);

        var maskinportenTokenResponse = await _maskinPortenTokenGenerator.RequestToken(token);
        var jsonDoc = JsonDocument.Parse(maskinportenTokenResponse);
        var root = jsonDoc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString();
        Assert.NotNull(accessToken);

        var altinnToken = await _platformAuthenticationClient.GetExchangeToken(accessToken);
        Assert.NotEmpty(altinnToken);
    }

    [Fact]
    public async Task GetBearerToken()
    {
        var token = await _maskinPortenTokenGenerator.GetMaskinportenBearerToken();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }
}