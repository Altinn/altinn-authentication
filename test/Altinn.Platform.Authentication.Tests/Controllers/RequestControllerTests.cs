using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Extensions;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;
#nullable enable

public class RequestControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
    : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
    
    private readonly Mock<IUserProfileService> _userProfileService = new();
    private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

    private readonly Mock<TimeProvider> timeProviderMock = new();
    private readonly Mock<IGuidService> guidService = new();
    private readonly Mock<IEventsQueueClient> _eventQueue = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        bool enableOidc = false;
        bool forceOidc = false;
        string defaultOidc = "altinn";

        string configPath = GetConfigPath();

        WebHostBuilder builder = new();

        builder.ConfigureAppConfiguration((context, conf) =>
        {
            conf.AddJsonFile(configPath);
        });

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
        services.AddSingleton<ISystemUserService, SystemUserServiceMock>();    
        services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
        services.AddSingleton<IRequestSystemUser, RequestSystemUserService>();
        SetupDateTimeMock();
        SetupGuidMock();
    }

    [Fact]
    public async Task Request_Create_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddTestTokenToClient(client);

        string endpoint = $"/authentication/api/v1/systemuser/request";

        // Arrange
        CreateRequestSystemUser req = new() 
        {
            ExternalRef = "external",
            SystemId = "the_matrix",
            PartyOrgNo = "1234567",
            Rights = []
        };
         
        HttpResponseMessage message = await client.PostAsync(token, endpoint, JsonContent.Create(req));
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);       
        
        CreateRequestSystemUserResponse? res = await message.Content.ReadFromJsonAsync<CreateRequestSystemUserResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);
    }

    [Fact]
    public async Task Request_Create_UnAuthorized()
    {
        HttpClient client = CreateClient();
        string endpoint = $"/authentication/api/v1/systemuser/request";

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "simen_test",
            PartyOrgNo = "1234567",
            Rights = []
        };

        HttpResponseMessage message = await client.PostAsync(string.Empty, endpoint, JsonContent.Create(req));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, message.StatusCode);
    }

    [Fact]
    public async Task Get_Request_ByGuid_Ok()
    {
        HttpClient client = CreateClient();
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid testId = Guid.NewGuid();
        string endpoint = $"/authentication/api/v1/systemuser/request/{testId}";

        HttpResponseMessage message = await client.GetAsync(token, endpoint);
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        CreateRequestSystemUserResponse? res = await message.Content.ReadFromJsonAsync<CreateRequestSystemUserResponse>();
        Assert.True(res is not null);
        Assert.Equal(testId, res.Id);
    }

    [Fact]
    public async Task Get_Request_ByExternalRef_Ok()
    {
        HttpClient client = CreateClient();
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        string testSystem = "Test_System";
        string orgno = "123456789";
        string testExternalRef = "Test_ExternalRef";

        // Create a new Request
        string endpoint = $"/authentication/api/v1/systemuser/request";

        CreateRequestSystemUser req = new()
        {
            ExternalRef = testExternalRef,
            SystemId = testSystem,
            PartyOrgNo = orgno,
            Rights = []
        };

        _ = await client.PostAsync(token, endpoint, JsonContent.Create(req));

        // Get the Request
        endpoint = $"/authentication/api/v1/systemuser/request/{testSystem}/{orgno}/{testExternalRef}";

        HttpResponseMessage message = await client.GetAsync(token, endpoint);
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        CreateRequestSystemUserResponse? res = await message.Content.ReadFromJsonAsync<CreateRequestSystemUserResponse>();
        Assert.True(res is not null);
        Assert.Equal(testSystem + testExternalRef, res.SystemId + res.ExternalRef);
    }

    private void SetupDateTimeMock()
    {
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
    }

    private void SetupGuidMock()
    {
        guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
    }

    private static string GetConfigPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, $"../../../appsettings.json");
    }

    private async Task<HttpResponseMessage> CreateSystemRegister(HttpClient client, string token)
    {
        string data = File.ReadAllText("Data/SystemRegister/Json/SystemRegister.json");
        JsonContent content = JsonContent.Create(data);
        var res = await client.PostAsync(token, $"/authentication/api/v1/systemregister/system/", content);
        return res;
    }

    private static string AddTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn.authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }

    private async Task<HttpResponseMessage> CreateSystemRegister(string dataFileName)
    {
        HttpClient client = CreateClient();
        string[] prefixes = { "altinn", "digdir" };
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        Stream dataStream = File.OpenRead(dataFileName);
        StreamContent content = new StreamContent(dataStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpRequestMessage request = new(HttpMethod.Post, $"/authentication/api/v1/systemregister/system/");
        request.Content = content;
        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response;
    }
}
