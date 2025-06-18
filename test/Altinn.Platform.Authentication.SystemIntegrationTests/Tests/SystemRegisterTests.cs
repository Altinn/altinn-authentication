using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.SystemRegister;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
using Xunit;
using Xunit.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

    private static async Task<string> GetRequestBodyWithReplacements(TestState testState, string filePath)
    {
        var fileContent = await Helper.ReadFile(filePath);
        return fileContent
            .Replace("{vendorId}", testState.VendorId)
            .Replace("{Name}", testState.Name)
            .Replace("{clientId}", testState.ClientId)
            .Replace("{redirectUrl}", testState.RedirectUrl);
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
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor) //Matches the maskinporten settings
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithName("SystemRegister e2e Tests: " + Guid.NewGuid())
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();

        // Act
        var response = await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var systems = await _systemRegisterClient.GetSystemsAsync(maskinportenToken);
        var isFound = systems.Exists(system => system.SystemId.Equals(teststate.SystemId));
        Assert.True(isFound, $"Could not find System that was created with Systemid: {teststate.SystemId}");

        // Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    /// <summary>
    /// Verify that you can post and create a system in Systemregister:
    /// https://github.com/Altinn/altinn-authentication/issues/575
    /// Requires Maskinporten token and valid vendor / organization id
    /// </summary>
    [Fact]
    public async Task CreateNewSystemWithAppReturns200Ok()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithName("SystemRegister e2e Tests With App: " + Guid.NewGuid())
            .WithClientId(Guid.NewGuid()
                .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor) //Matches the maskinporten settings
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();

        // Act
        var response = await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var systems = await _systemRegisterClient.GetSystemsAsync(maskinportenToken);
        var isFound = systems.Exists(system => system.SystemId.Equals(teststate.SystemId));
        Assert.True(isFound, $"Could not find System that was created with Systemid: {teststate.SystemId}");

        // Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    [Fact]
    public async Task GetSystemRegisterReturns200Ok()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        // Act
        var response =
            await _platformClient.GetAsync(Endpoints.GetAllSystemsFromRegister.Url(), maskinportenToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Verify that the correct number of rights are returned from the defined system
    [Fact]
    public async Task ValidateRights()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithName("SystemRegister e2e Tests For Validating Rights " + Guid.NewGuid())
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "resource_nonDelegable_enkeltrettighet", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBody = teststate.GenerateRequestBody();

        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Act
        var response =
            await _platformClient.GetAsync(Endpoints.GetSystemRegisterRights.Url().Replace("{systemId}", teststate.SystemId), maskinportenToken);

        var rightsFromApiResponse = await response.Content.ReadFromJsonAsync<List<Right>>();
        Assert.NotNull(rightsFromApiResponse);
        Assert.Equal(3, rightsFromApiResponse.Count);

        // Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    /// <summary>
    /// Verify registered system gets deleted (soft delete)
    /// </summary>
    [Fact]
    public async Task DeleteRegisteredSystemReturns200Ok()
    {
        // Prepares
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithName("SystemRegister e2e Tests - Delete" + Guid.NewGuid())
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        //post system to Systemregister
        var requestBody = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);

        // Act
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);

        // Assert system is not found
        var systems = await _systemRegisterClient.GetSystemsAsync(maskinportenToken);
        var isFound = systems.Exists(system => system.SystemId.Equals(teststate.SystemId));
        Assert.False(isFound);
    }

    [Fact] //Relevant Bug reported - https://github.com/Altinn/altinn-authentication/issues/856
    public async Task UpdateRegisteredSystemReturns200Ok()
    {
        // Prepares
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithName("SystemRegister e2e Tests Put " + Guid.NewGuid())
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);

        //Prepare 
        var requestBody =
            await GetRequestBodyWithReplacements(teststate, "Resources/Testdata/Systemregister/UnitTestFilePut.json");

        // Act
        var response =
            await _platformClient.PutAsync($"{Endpoints.UpdateVendorSystemRegister.Url()}".Replace("{systemId}", teststate.SystemId), requestBody, maskinportenToken);

        // Assert
        await Common.AssertResponse(response, HttpStatusCode.OK);
        // Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get =
            await _platformClient.GetAsync($"{Endpoints.GetSystemRegisterById.Url()}".Replace("{systemId}", teststate.SystemId), maskinportenToken);

        //More asserts should be added, but there are known bugs right now regarding validation of rights 
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var getForVendor =
            await _platformClient.GetAsync($"{Endpoints.GetVendorSystemRegisterById.Url()}".Replace("{systemId}", teststate.SystemId), maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, getForVendor.StatusCode);

        //Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    [Fact]
    public async Task VerifySystemRegistergetSystemsIsOk()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var systems = await _systemRegisterClient.GetSystemsAsync(maskinportenToken);

        //verify endpoint responds ok
        Assert.NotNull(systems);
    }

    [Fact]
    public async Task GetBySystemIdReturns200Ok()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithName(Guid.NewGuid().ToString())
            .WithToken(maskinportenToken);

        await _systemRegisterClient.PostSystem(teststate.GenerateRequestBody(), maskinportenToken);

        var resp = await _systemRegisterClient.getBySystemId(teststate.SystemId, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());

        //Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    [Fact]
    public async Task GetBySystemIdReturns403IfNotValidOrg()
    {
        // Prepares
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithName(Guid.NewGuid().ToString())
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = teststate.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);

        //Fetch token for different org
        var illegalOrgToken = await _platformClient.GetEnterpriseAltinnToken("214270102", "altinn:authentication/systemregister.write");

        // Get system
        var resp = await _systemRegisterClient.getBySystemId(teststate.SystemId, illegalOrgToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());

        // Cleanup
        await _systemRegisterClient.DeleteSystem(teststate.SystemId, maskinportenToken);
    }

    [Fact]
    public async Task UpdateRightsInSystemForVendorReturns200Ok()
    {
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithName("SystemRegister e2e Tests: Update rights: " + Guid.NewGuid())
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "resource_nonDelegable_enkeltrettighet", id: "urn:altinn:resource")
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
            await _platformClient.GetAsync($"{Endpoints.GetVendorSystemRegisterById.Url()}".Replace("{systemId}", teststate.SystemId), maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, getForVendor.StatusCode);

        var stringBody = await getForVendor.Content.ReadAsStringAsync();

        Assert.Contains("authentication-e2e-test", stringBody);
        Assert.Contains("vegardtestressurs", stringBody);
        Assert.DoesNotContain("resource_nonDelegable_enkeltrettighet", stringBody);
    }

    [Fact]
    public async Task PostSystemWithAccessPackage()
    {
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new TestState("Resources/Testdata/Systemregister/AccessPackageSystemRegister.json")
            .WithRedirectUrl("https://altinn.no")
            .WithClientId(Guid.NewGuid().ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithName(Guid.NewGuid().ToString())
            .WithToken(maskinportenToken);

        var resp = await _systemRegisterClient.PostSystem(teststate.GenerateRequestBody(), maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PutSystemWithAccessPackage()
    {
        // Prepare with basic access package
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        TestState state = _platformClient.SystemRegisterClient.CreateStandardSystemWithAccessPackage(maskinportenToken);
        await _systemRegisterClient.PostSystem(state.GenerateRequestBody(), maskinportenToken);

        // Update with new accessPackages
        List<AccessPackageDto> requestBodyPut =
        [
            new() { Urn = "urn:altinn:accesspackage:post-og-telekommunikasjon" },
            new() { Urn = "urn:altinn:accesspackage:dokumentbasert-tilsyn" },
            new() { Urn = "urn:altinn:accesspackage:infrastruktur" },
            new() { Urn = "urn:altinn:accesspackage:patent-varemerke-design" },
            new() { Urn = "urn:altinn:accesspackage:tilskudd-stotte-erstatning" },
            new() { Urn = "urn:altinn:accesspackage:mine-sider-kommune" },
            new() { Urn = "urn:altinn:accesspackage:politi-og-domstol" },
            new() { Urn = "urn:altinn:accesspackage:rapportering-statistikk" },
            new() { Urn = "urn:altinn:accesspackage:forskning" },
            new() { Urn = "urn:altinn:accesspackage:folkeregister" },
            new() { Urn = "urn:altinn:accesspackage:maskinporten-scopes" },
            new() { Urn = "urn:altinn:accesspackage:maskinlesbare-hendelser" },
            new() { Urn = "urn:altinn:accesspackage:maskinporten-scopes-nuf" }
        ];

        var jsonString = JsonSerializer.Serialize(requestBodyPut);
        var url = Endpoints.UpdateVendorAccessPackages.Url()?.Replace("{systemId}", state.SystemId);
        var responsePut = await _platformClient.PutAsync(url, jsonString, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, responsePut.StatusCode);

        // Get system to see what it contains
        var resp = await _platformClient.SystemRegisterClient.getBySystemId(state.SystemId, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updatedSystem = await resp.Content.ReadAsStringAsync();

        var jsonDoc = JsonDocument.Parse(updatedSystem);
        var accessPackages = jsonDoc.RootElement.GetProperty("accessPackages");

        // Actual Urns from resposne
        List<AccessPackageDto> actualUrns = accessPackages.EnumerateArray()
            .Select(p => p.GetProperty("urn").GetString())
            .Where(urn => urn != null)
            .Select(urn => new AccessPackageDto { Urn = urn! })
            .ToList();
        
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(actualUrns.Count, requestBodyPut.Count);
        Assert.Equal(actualUrns, requestBodyPut);
    }
}