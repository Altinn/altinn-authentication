using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    public static async Task<string> GetRequestBodyWithReplacements(SystemRegisterHelper systemRegisterHelper,
        string filePath)
    {
        var fileContent = await Helper.ReadFile(filePath);
        return fileContent
            .Replace("{vendorId}", systemRegisterHelper.VendorId)
            .Replace("{Name}", systemRegisterHelper.Name)
            .Replace("{clientId}", systemRegisterHelper.ClientId)
            .Replace("{redirectUrl}", systemRegisterHelper.RedirectUrl);
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
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor) //Matches the maskinporten settings
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "resource_nonDelegable_enkeltrettighet", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();

        // Act
        var response = await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSystemRegisterReturns200Ok()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        // Act
        var response =
            await _platformClient.GetAsync("v1/systemregister", maskinportenToken);


        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Verify that the correct number of rights are returned from the defined system
    [Fact]
    public async Task ValidateRights()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor("312605031")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "resource_nonDelegable_enkeltrettighet", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();

        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Act
        var response =
            await _platformClient.GetAsync(
                $"v1/systemregister/{teststate.SystemId}/rights", maskinportenToken);

        var rightsFromApiResponse = await response.Content.ReadFromJsonAsync<List<Right>>();
        Assert.NotNull(rightsFromApiResponse);
        Assert.Equal(3, rightsFromApiResponse.Count);
    }

    /// <summary>
    /// Verify registered system gets deleted (soft delete)
    /// </summary>
    [Fact]
    public async Task DeleteRegisteredSystemReturns200Ok()
    {
        // Prepares
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        //post system to Systemregister
        var requestBody = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Act
        var respons = await _platformClient.Delete(
            $"v1/systemregister/vendor/{teststate.SystemId}", teststate.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    //[Fact] Bug reported - https://github.com/Altinn/altinn-authentication/issues/856
    public async Task UpdateRegisteredSystemReturns200Ok()
    {
        // Prepares
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor("312605031")
            .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);

        //Prepare 
        var requestBody =
            await GetRequestBodyWithReplacements(teststate, "Resources/Testdata/Systemregister/UnitTestFilePut.json");

        // Act
        var response =
            await _platformClient.PutAsync($"v1/systemregister/vendor/{teststate.SystemId}",
                requestBody, maskinportenToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get =
            await _platformClient.GetAsync($"v1/systemregister/{teststate.SystemId}",
                maskinportenToken);

        //More asserts should be added, but there are known bugs right now regarding validation of rights 
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task DeleteEverySystemCreatedByEndToEndTests()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenToken();
        var systemIds = await _systemRegisterClient.GetSystemsAsync(maskinportenToken);

        var idsToDelete = systemIds.FindAll(system => system.SystemVendorOrgNumber.Equals(_platformClient.EnvironmentHelper.Vendor));
        
        foreach (var systemDto in idsToDelete)
        {
            _outputHelper.WriteLine("Attempting to delete system with id: " + systemDto.SystemId);
            // Act
            var respons = await _platformClient.Delete(
                $"v1/systemregister/vendor/{systemDto.SystemId}", maskinportenToken);
        
            // Assert
            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        }
        
    }
}