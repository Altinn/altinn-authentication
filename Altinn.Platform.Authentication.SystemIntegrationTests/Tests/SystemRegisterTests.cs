using System.Net;
using System.Net.Http.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Tests relevant for "Systemregister": https://github.com/Altinn/altinn-authentication/issues/575
/// </summary>

[Trait("Category", "IntegrationTest")]
public class SystemRegisterTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemRegisterClient _systemRegisterClient;

    // Forbedringer
    // - Unngå å skrive til systemregisteret hver gang

    /// <summary>
    /// Systemregister tests
    /// </summary>
    /// <param name="outputHelper">For test logging purposes</param>
    public SystemRegisterTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    public static async Task<string> GetRequestBodyWithReplacements(SystemRegisterState systemRegisterState,
        string? filePath)
    {
        var fileContent = await Helper.ReadFile(filePath);
        return fileContent
            .Replace("{vendorId}", systemRegisterState.VendorId)
            .Replace("{Name}", systemRegisterState.Name)
            .Replace("{clientId}", systemRegisterState.ClientId);
    }

    /// <summary>
    /// Verify that you can post and create a system in Systemregister:
    /// https://github.com/Altinn/altinn-authentication/issues/575
    /// Requires Maskinporten token and valid vendor / organization id
    /// </summary>
    [Fact]
    public async Task CreateNewSystemReturns200Ok()
    {
        // Prepare
        var token = await _platformClient.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        // Act
        var response = await _systemRegisterClient.PostSystem(teststate);

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
        var token = await _platformClient.GetTokenForClient("SystemRegisterClient");

        // Act
        var response =
            await _platformClient.GetAsync("/authentication/api/v1/systemregister", token);
        
        _outputHelper.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Verify that the correct number of rights are returned from the defined system
    [Fact]
    public async Task ValidateRights()
    {
        // Prepare
        var token = await _platformClient.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        await _systemRegisterClient.PostSystem(teststate);

        // Act
        var response =
            await _platformClient.GetAsync(
                $"/authentication/api/v1/systemregister/{teststate.SystemId}/rights", token);
        var rights = await response.Content.ReadFromJsonAsync<List<Right>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(teststate.Rights.First().Resource!.First().Value, rights!.First().Resource!.First().Value);
    }

    /// <summary>
    /// Verify registered system gets deleted
    /// </summary>
    [Fact]
    public async Task DeleteRegisteredSystemReturns200Ok()
    {
        // Prepare
        var token = await _platformClient.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        //post system to Systemregister
        await _systemRegisterClient.PostSystem(teststate);

        // Act
        var respons = await _platformClient.Delete(
            $"/authentication/api/v1/systemregister/vendor/{teststate.SystemId}", token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [Fact]
    public async Task UpdateRegisteredSystemReturns200Ok()
    {
        // Prerequisite-step
        var token = await _platformClient.GetTokenForClient("SystemRegisterClient");

        var teststate = new SystemRegisterState()
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(token);

        //post system to Systemregister
        await _systemRegisterClient.PostSystem(teststate);

        //Prepare 
        var requestBody =
            await GetRequestBodyWithReplacements(teststate, "Resources/Testdata/Systemregister/UnitTestfilePut.json");

        // Act
        var response =
            await _platformClient.PutAsync($"/authentication/api/v1/systemregister/vendor/{teststate.SystemId}",
                requestBody, token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get =
            await _platformClient.GetAsync($"/authentication/api/v1/systemregister/{teststate.SystemId}",
                token);

        //More asserts should be added, but there are known bugs right now regarding validation of rights 
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }
}