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

    private async Task<string> GetRequestBodyWithReplacements(SystemRegisterState systemRegisterState, string filePath)
    {
        var fileContent = await Helper.ReadFile(filePath);
        return fileContent
            .Replace("{vendorId}", systemRegisterState.VendorId)
            .Replace("{Name}", systemRegisterState.Name)
            .Replace("{clientId}", systemRegisterState.ClientId);
    }

    [Fact]
    public async Task CreateNewSystemReturns200Ok()
    {
        // Prepare
        const string filePath = "Resources/Testdata/Systemregister/CreateNewSystem.json";
        const string endpoint = "authentication/api/v1/systemregister/vendor";
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        var testState = new SystemRegisterState("312605031", Guid.NewGuid().ToString());
        var requestBody = await GetRequestBodyWithReplacements(testState, filePath);

        // Act
        var response = await _platformAuthenticationClient.PostAsync(endpoint, requestBody, token);

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
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        // Act
        var response =
            await _platformAuthenticationClient.GetAsync("/authentication/api/v1/systemregister", token);

        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Verify that the correct number of rights are returned from the defined system
    [Fact]
    public async Task ValidateRights()
    {
        // Prepare
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");
        var teststate = await PostSystem(token);

        // Act
        var response =
            await _platformAuthenticationClient.GetAsync(
                $"/authentication/api/v1/systemregister/{teststate.SystemId}/rights", token);
        var rights = await response.Content.ReadFromJsonAsync<List<Right>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(teststate.Rights.First().Resource.First().Value, rights!.First().Resource.First().Value);
    }

    /// <summary>
    /// Verify registered system gets deleted
    /// </summary>
    [Fact]
    public async Task DeleteRegisteredSystemReturns200Ok()
    {
        // Prepare
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        //post system to Systemregister
        var testState = await PostSystem(token);

        // Act
        var respons = await _platformAuthenticationClient.Delete(
            $"/authentication/api/v1/systemregister/vendor/{testState.SystemId}", token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    private async Task<SystemRegisterState> PostSystem(string token)
    {
        // Prepare
        const string filePath = "Resources/Testdata/Systemregister/CreateNewSystem.json";
        const string endpoint = "authentication/api/v1/systemregister/vendor";

        var testState = new SystemRegisterState("312605031", Guid.NewGuid().ToString());
        var requestBody = await GetRequestBodyWithReplacements(testState, filePath);

        var response = await _platformAuthenticationClient.PostAsync(endpoint, requestBody, token);

        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return testState;
    }

    [Fact]
    public async Task UpdateRegisteredSystemReturns200Ok()
    {
        // Prepare
        var token = await _platformAuthenticationClient.GetTokenForClient("SystemRegisterClient");

        //post system to Systemregister
        var testState = await PostSystem(token);
        var requestBody =
            await GetRequestBodyWithReplacements(testState, "Resources/Testdata/Systemregister/UnitTestfilePut.json");

        // Act
        var response =
            await _platformAuthenticationClient.PutAsync(
                $"/authentication/api/v1/systemregister/vendor/{testState.SystemId}",
                requestBody, token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get =
            await _platformAuthenticationClient.GetAsync($"/authentication/api/v1/systemregister/{testState.SystemId}",
                token);

        _outputHelper.WriteLine($"updated system  {await get.Content.ReadAsStringAsync()}");
    }
}