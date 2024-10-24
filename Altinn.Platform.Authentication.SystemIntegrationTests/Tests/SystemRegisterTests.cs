using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.AccessManagement.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Tests relevant for "Systemregister": https://github.com/Altinn/altinn-authentication/issues/575
/// </summary>
public class SystemRegisterTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Teststate _teststate;
    private readonly PlatformAuthenticationClient _platformAuthenticationClient;
    private readonly SystemRegisterClient _systemRegisterClient;

    /// <summary>
    /// Systemregister tests
    /// </summary>
    /// <param name="outputHelper">For test logging purposes</param>
    public SystemRegisterTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _systemRegisterClient = new SystemRegisterClient(_outputHelper);
        _teststate = new Teststate();
        _platformAuthenticationClient = new PlatformAuthenticationClient();
    }

    private async Task<HttpResponseMessage> CreateNewSystem(string token, string vendorId = "312605031")
    {
        const string endpoint = "authentication/api/v1/systemregister/vendor";
        _teststate.vendorId = vendorId;

        var randomName = Helper.GenerateRandomString(10);
        var testfile = await Helper.ReadFile("Resources/Testdata/Systemregister/CreateNewSystem.json");

        testfile = testfile
            .Replace("{vendorId}", vendorId)
            .Replace("{randomName}", randomName)
            .Replace("{clientId}", Guid.NewGuid().ToString());

        return await _platformAuthenticationClient.PostAsync(endpoint, testfile, token);
    }

    /// <summary>
    /// AK1 - Opprett system i systemregisteret
    /// </summary>
    [Fact]
    public async Task CreateNewSystemReturns200Ok()
    {
        var maskinportenClient =
            _platformAuthenticationClient.EnvironmentHelper.GetMaskinportenClientByName("SystemRegisterClient");

        var token =
            await _platformAuthenticationClient._maskinPortenTokenGenerator.GetMaskinportenBearerToken(
                maskinportenClient);

        // Act
        var response = await CreateNewSystem(token);

        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test Get SystemRegister
    /// </summary>
    [Fact]
    public async Task GetSystemRegisterReturns200Ok()
    {
        var maskinportenClient =
            _platformAuthenticationClient.EnvironmentHelper.GetMaskinportenClientByName("SystemRegisterClient");
        var token =
            await _platformAuthenticationClient._maskinPortenTokenGenerator.GetMaskinportenBearerToken(
                maskinportenClient);

        // Act
        var response =
            await _platformAuthenticationClient.GetAsync("/authentication/api/v1/systemregister", token);

        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Verify that the correct number of rights are returned ffrom the defined system
    [Fact]
    public async Task ValidateRights()
    {
        // Prepare
        var maskinportenClient =
            _platformAuthenticationClient.EnvironmentHelper.GetMaskinportenClientByName("SystemRegisterClient");
        var token =
            await _platformAuthenticationClient._maskinPortenTokenGenerator.GetMaskinportenBearerToken(
                maskinportenClient);

        // the vendor of the system, could be visma
        const string vendorId = "312605031";
        var randomName = Helper.GenerateRandomString(15);

        var testfile = await Helper.ReadFile("Resources/Testdata/Systemregister/CreateNewSystem.json");

        testfile = testfile
            .Replace("{vendorId}", vendorId)
            .Replace("{randomName}", randomName)
            .Replace("{clientId}", Guid.NewGuid().ToString());

        var systemRequest = JsonSerializer.Deserialize<RegisterSystemRequest>(testfile);
        await _systemRegisterClient.CreateNewSystem(systemRequest, token, randomName, vendorId);

        // Act
        var response =
            await _platformAuthenticationClient.GetAsync(
                $"/authentication/api/v1/systemregister/{vendorId}_{randomName}/rights", token);
        var rights = await response.Content.ReadFromJsonAsync<List<Right>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(systemRequest.Rights.First().Resource.First().Value, rights!.First().Resource.First().Value);
    }

    /// <summary>
    /// Verify registered system gets deleted
    /// </summary>
    [Fact] //Todo: This currently fails
    public async Task DeleteRegisteredSystemReturns200Ok()
    {
        // Prepare
        var maskinportenClient =
            _platformAuthenticationClient.EnvironmentHelper.GetMaskinportenClientByName("SystemRegisterClient");
        var token =
            await _platformAuthenticationClient._maskinPortenTokenGenerator.GetMaskinportenBearerToken(
                maskinportenClient);

        // the vendor of the system, could be visma
        const string vendorId = "312605031";
        var randomName = Helper.GenerateRandomString(15);

        var testfile = await Helper.ReadFile("Resources/Testdata/Systemregister/CreateNewSystem.json");

        testfile = testfile
            .Replace("{vendorId}", vendorId)
            .Replace("{randomName}", randomName)
            .Replace("{clientId}", Guid.NewGuid().ToString());

        var systemRequest = JsonSerializer.Deserialize<RegisterSystemRequest>(testfile);
        await _systemRegisterClient.CreateNewSystem(systemRequest!, token, randomName, vendorId);

        // Act
        var respons = await _platformAuthenticationClient.Delete(
            $"/authentication/api/v1/systemregister/vendor/{vendorId}_{randomName}",
            token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }
}