using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Errors;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;

public class UpdateSystemTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture) : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Mock<IUserProfileService> _userProfileService = new();
    private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

    private readonly Mock<TimeProvider> timeProviderMock = new Mock<TimeProvider>();
    private readonly Mock<IGuidService> guidService = new Mock<IGuidService>();
    private readonly Mock<IEventsQueueClient> _eventQueue = new Mock<IEventsQueueClient>();

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string GetConfigPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, $"../../../appsettings.json");
    }

    // Todo: Refactor this and move this to its own fixture
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        bool enableOidc = false;
        bool forceOidc = false;
        string defaultOidc = "altinn";

        string configPath = GetConfigPath();

        WebHostBuilder builder = new();

        builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath)
            .Build();

        configuration.GetSection("GeneralSettings:EnableOidc").Value = enableOidc.ToString();
        configuration.GetSection("GeneralSettings:ForceOidc").Value = forceOidc.ToString();
        configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = defaultOidc;

        IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

        services.Configure<GeneralSettings>(generalSettingSection);
        services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
        services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
        services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
        services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
        services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
        services.AddSingleton(_eventQueue.Object);
        services.AddSingleton(timeProviderMock.Object);
        services.AddSingleton(guidService.Object);
        services.AddSingleton<IUserProfileService>(_userProfileService.Object);
        services.AddSingleton<ISblCookieDecryptionService>(_sblCookieDecryptionService.Object);
        services.AddSingleton<ISystemUserService, SystemUserService>();
        services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
        services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
        services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
        SetupDateTimeMock();
        SetupGuidMock();
    }

    private void SetupDateTimeMock()
    {
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
    }

    private void SetupGuidMock()
    {
        guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
    }

    [Fact]
    public async Task SystemRegister_Update_System_UnchangedClientId_Test()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
        HttpResponseMessage response = await CreateSystemRegister(originalSystem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Keep using the same clientId, but update something else
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem, true);
        var resp = await PutSystemRegisterAsync(updatedSystem, systemId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        HttpResponseMessage getSystemResponse = await GetSystemRegister(systemId);
        Assert.Equal(HttpStatusCode.OK, getSystemResponse.StatusCode);

        // Assert new system contains the same clientId
        RegisteredSystemResponse actualUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getSystemResponse.Content.ReadAsStringAsync(), _options);
        Assert.True(actualUpdatedSystem.ClientId.Count == clientIdsInFirstSystem.Count);
        Assert.Contains(clientIdsInFirstSystem[0], actualUpdatedSystem.ClientId[0]);
        Assert.True(actualUpdatedSystem.IsVisible);
    }

    [Fact]
    public async Task SystemRegister_Update_System_Remove_All_Test()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
        HttpResponseMessage response = await CreateSystemRegister(originalSystem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Remove clientIds
        List<string> newClientIds = [];
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds);
        var resp = await PutSystemRegisterAsync(updatedSystem, systemId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        HttpResponseMessage getSystemResponse = await GetSystemRegister(systemId);
        Assert.Equal(HttpStatusCode.OK, getSystemResponse.StatusCode);

        // Assert new system contains the two clientIds, and only those
        RegisteredSystemResponse actualUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getSystemResponse.Content.ReadAsStringAsync(), _options);

        //TODo - er dette lov?
    }

    [Fact]
    public async Task SystemRegister_DuplicateClientIds_Test()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        List<string> clientIdsInFirstSystem = ["456", "8910"];
        RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
        HttpResponseMessage response = await CreateSystemRegister(originalSystem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Duplicate clientIds
        List<string> newClientIds = ["123", "123"];
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds);
        var resp = await PutSystemRegisterAsync(updatedSystem, systemId);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        
        string content = await resp.Content.ReadAsStringAsync();
        Assert.Contains(ValidationErrors.SystemRegister_Duplicate_ClientIds.Detail, content);
    }

    [Fact]
    public async Task SystemRegister_UnmatchedRequestBodyAndSystemId()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
        HttpResponseMessage response = await CreateSystemRegister(originalSystem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<string> newClientIds = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds);

        // Expecting bad request here
        var resp = await PutSystemRegisterAsync(updatedSystem, "991825827_does_not_match_request_bodys_system_id");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        // Read the content as string
        string content = await resp.Content.ReadAsStringAsync();
        Assert.Equal("SystemId in request body doesn't match systemId in Url", content);
    }

    [Fact]
    public async Task SystemRegister_SystemNotFound_Test()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
        HttpResponseMessage response = await CreateSystemRegister(originalSystem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Try to put on system that does not exist
        const string putSystemId = "SystemIdThatDoesNotExist";
        List<string> newClientIds = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(putSystemId, newClientIds);

        // Expecting bad request here
        var resp = await PutSystemRegisterAsync(updatedSystem, putSystemId);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // Read the content as string
        string content = await resp.Content.ReadAsStringAsync();
        Assert.Equal($"System with ID '{putSystemId}' not found.", content);
    }

    [Fact]
    public async Task SystemRegister_ClientIdExists_Test()
    {
        // Prepare
        const string systemId = "991825827_the_matrix";
        const string systemIdSecondSystem = "991825827_snowman";

        List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
        HttpResponseMessage responseFirst = await CreateAndAssertSystemAsync(systemId, clientIdsInFirstSystem);
        Assert.Equal(HttpStatusCode.OK, responseFirst.StatusCode);

        List<string> clientIdsSecondSystem = ["ClientIdForSecondSystem"];
        await CreateAndAssertSystemAsync(systemIdSecondSystem, clientIdsSecondSystem);

        // Running update with one new clientId and also one old from a second system
        List<string> newClientIds = ["NewClientIdToUpdate", clientIdsSecondSystem[0]];
        RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds);

        // Expecting bad request here
        HttpResponseMessage resp = await PutSystemRegisterAsync(updatedSystem, systemId);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        string content = await resp.Content.ReadAsStringAsync();
        Assert.Contains(ValidationErrors.SystemRegister_ClientID_Exists.Detail, content);
    }

    private async Task<HttpResponseMessage> CreateAndAssertSystemAsync(string systemId, List<string> clientIds)
    {
        var request = CreateSystemRegisterRequest(systemId, clientIds);
        HttpResponseMessage response = await CreateSystemRegister(request);
        return response;
    }

    private async Task<HttpResponseMessage> CreateSystemRegister(RegisterSystemRequest registerSystemRequest)
    {
        HttpClient client = CreateClient();

        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken(
            "digdir",
            "991825827",
            "altinn:authentication/systemregister.admin",
            prefixes);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string json = JsonSerializer.Serialize(registerSystemRequest, _options);
        HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpRequestMessage request = new(HttpMethod.Post, "/authentication/api/v1/systemregister/vendor/")
        {
            Content = content
        };

        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response;
    }

    private async Task<HttpResponseMessage> PutSystemRegisterAsync(RegisterSystemRequest updateDto, string systemIdPath)
    {
        HttpClient client = CreateClient();

        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var json = JsonSerializer.Serialize(updateDto, options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemIdPath}/")
        {
            Content = content
        };

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    public static RegisterSystemRequest CreateSystemRegisterRequest(string systemId, List<string> clientIds, bool isVisible = false)
    {
        return new RegisterSystemRequest
        {
            Id = systemId,
            Vendor = new VendorInfo
            {
                Authority = "iso6523-actorid-upis",
                ID = "0192:991825827"
            },
            Name = new Dictionary<string, string>
            {
                { "nb", "The Matrix" },
                { "en", "The Matrix" },
                { "nn", "The Matrix" }
            },
            Description = new Dictionary<string, string>
            {
                { "nb", "Test system for Put" },
                { "en", "Test system for Put" },
                { "nn", "Test system for Put" }
            },
            Rights = new List<Right>
            {
                new()
                {
                    Resource = new List<AttributePair>
                    {
                        new()
                        {
                            Id = "urn:altinn:resource",
                            Value = "ske-krav-og-betalinger"
                        }
                    }
                }
            },
            ClientId = clientIds,
            AllowedRedirectUrls = new List<Uri>
            {
                new("https://vg.no"),
                new("https://nrk.no"),
                new("https://altinn.no")
            },
            IsVisible = isVisible
        };
    }

    private async Task<HttpResponseMessage> GetSystemRegister(string systemId)
    {
        HttpClient client = CreateClient();
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
        HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

        return getResponse;
    }
}