using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.AccessManagement.Tests.Mocks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Extensions;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;
#nullable enable

public class RequestControllerTests(
    DbFixture dbFixture, 
    WebApplicationFixture webApplicationFixture)
    : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
    
    private readonly Mock<IUserProfileService> _userProfileService = new();
    private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

    private readonly Mock<TimeProvider> timeProviderMock = new();
    private readonly Mock<IGuidService> guidService = new();
    private readonly Mock<IEventsQueueClient> _eventQueue = new();
    private int _paginationSize;

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
        IConfigurationSection paginationSettingSection = configuration.GetSection("PaginationOptions");

        services.Configure<GeneralSettings>(generalSettingSection);        
        services.Configure<PaginationOptions>(paginationSettingSection);
        _paginationSize = configuration.GetValue<int>("PaginationOptions:Size");
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
        services.AddSingleton<IPDP, PepWithPDPAuthorizationMock>();
        services.AddSingleton<IPartiesClient, PartiesClientMock>();
        services.AddSingleton<ISystemUserService, SystemUserService>();    
        services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
        services.AddSingleton<IRequestSystemUser, RequestSystemUserService>();
        services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
        services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
        services.AddSingleton<IRequestRepository, RequestRepository>();
        SetupDateTimeMock();
        SetupGuidMock();
    }

    [Fact]
    public async Task Request_Create_FirstAttempt_ReturnCreated()
    {
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ] 
        };

        // Arrange
        CreateRequestSystemUser req = new() 
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);       
        
        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);
        Assert.NotNull(res.ConfirmUrl);
    }

    [Fact]
    public async Task Request_Create_SecondAttempt_ReturnOK()
    {
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        // First attempt return Created
        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Second attempt return Created
        HttpRequestMessage request2 = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message2 = await client.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);

        RequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res2);
        Assert.Equal(req.ExternalRef, res2.ExternalRef);
        Assert.Equal(res.ConfirmUrl, res2.ConfirmUrl);
    }

    [Fact]
    public async Task Request_Create_Failed_WrongRights()
    {
        string dataFileName = "Data/SystemRegister/Json/SystemRegisterSubRights.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                },
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "finnesikke"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.BadRequest, message.StatusCode);                
    }

    [Fact]
    public async Task Request_Create_Succeed_SubResource()
    {
        string dataFileName = "Data/SystemRegister/Json/SystemRegisterSubRights.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                },
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger-2"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);
    }

    [Fact]
    public async Task Request_Create_UnAuthorized()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();

        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Unauthorized, message.StatusCode);
    }

    [Fact]
    public async Task Get_Request_ByGuid_Ok()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //Get by Guid
        HttpClient client2 = CreateClient();
        AddSystemUserRequesReadTestTokenToClient(client2);
        Guid testId = res.Id;
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";

        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        string debug = "pause_here";
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        RequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.True(res2 is not null);
        Assert.Equal(testId, res2.Id);
    }

    [Fact]
    public async Task Get_Client_Request_ByGuid_Ok()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        AccessPackage accessPackage = new AccessPackage();
        accessPackage.Urn = "urn:altinn:accesspackage:skattnaering";

        // Arrange
        CreateClientRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            AccessPackages = [accessPackage]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        ClientRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<ClientRequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //Get by Guid
        HttpClient client2 = CreateClient();
        AddSystemUserRequesReadTestTokenToClient(client2);
        Guid testId = res.Id;
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/client/{testId}";

        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        string debug = "pause_here";
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        ClientRequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<ClientRequestSystemResponse>();
        Assert.True(res2 is not null);
        Assert.Equal(testId, res2.Id);
    }

    [Fact]
    public async Task Get_Request_ByExternalRef_Ok()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);
        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Get the Request
        HttpClient client2 = CreateClient();
        AddSystemUserRequesReadTestTokenToClient(client2);
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/byexternalref/{req.SystemId}/{req.PartyOrgNo}/{req.ExternalRef}";

        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        RequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.True(res2 is not null);
        Assert.Equal(req.SystemId + req.PartyOrgNo + req.ExternalRef, res2.SystemId + res2.PartyOrgNo + res2.ExternalRef);
    }

    [Fact]
    public async Task Get_Request_By_Party_RequestId()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

        int partyId = 500000;

        string partyEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}";

        HttpRequestMessage partyReqMessage = new(HttpMethod.Get, partyEndpoint);
        HttpResponseMessage partyResponse = await client2.SendAsync(partyReqMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, partyResponse.StatusCode);

        RequestSystemResponse? requestGet = JsonSerializer.Deserialize<RequestSystemResponse>(await partyResponse.Content.ReadAsStringAsync());
        Assert.NotNull(requestGet);

        Assert.Equal(res.Id, requestGet.Id);
    }

    [Fact]
    public async Task Approve_Request_By_RequestId_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3,true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);
    }

    [Fact]
    public async Task Approve_Request_By_RequestId_Forbidden()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponseMessage.StatusCode);
    }

    [Fact]
    public async Task Approve_Request_WithApp_By_RequestId_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithApp.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "app_ttd_endring-av-navn-v2"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_system_with_app",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);
    }

    [Fact]
    public async Task Approve_Request_WithApp_And_Resource_By_RequestId_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithResourceAndApp.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        List<Right> right = new List<Right>();
        var rights = new List<Right>
            {
                new Right
                {
                    Resource = new List<AttributePair>
                    {
                        new AttributePair { Id = "urn:altinn:resource", Value = "app_ttd_endring-av-navn-v2" }
                    }
                },
                new Right
                {
                    Resource = new List<AttributePair>
                    {
                        new AttributePair { Id = "urn:altinn:resource", Value = "ske-krav-og-betalinger" }
                    }
                }
            };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_system_with_app_and_resource",
            PartyOrgNo = "910493353",
            Rights = rights
        };

        string serialized = JsonSerializer.Serialize(req);
        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);
    }

    [Fact]
    public async Task Approve_Request_Then_CheckRequest_IsOK()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        // Vendor checks afterwards that the Request is approved
        //Get by Guid
        HttpClient client3 = CreateClient();
        AddSystemUserRequesReadTestTokenToClient(client3);
        Guid testId = res.Id;
        string endpoint3 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";

        HttpResponseMessage message3 = await client3.GetAsync(endpoint3);
        string debug = "pause_here";
        Assert.Equal(HttpStatusCode.OK, message3.StatusCode);
        RequestSystemResponse? res3 = await message3.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.True(res3 is not null);
        Assert.Equal(testId, res3.Id);
        Assert.Equal("Accepted", res3.Status);
    }

    [Fact]
    public async Task Approve_Request_SecondTime_ReturnBadRequest() 
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        HttpRequestMessage requestAgain = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage messageAgain = await client.SendAsync(requestAgain, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.BadRequest, messageAgain.StatusCode);
    }

    [Fact]
    public async Task Create_Request_By_RequestId_DoubleRequest_ReturnOK()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Second Request
        HttpRequestMessage request2 = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message2 = await client.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead);

        // Return OK in stead of Created, signifying that the request already exists, and that the request is not created again.
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);       
    }

    [Fact]
    public async Task Approve_Request_By_RequestId_Fail()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500004;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponseMessage.StatusCode);
        ProblemDetails problem = await approveResponseMessage.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("DelegationCheck failed with unknown error.", problem.Detail);
    }

    [Fact]
    public async Task Approve_Request_By_RequestId_NotDelegated()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493355",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500005;

        string partyEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}";

        HttpRequestMessage partyReqMessage = new(HttpMethod.Get, partyEndpoint);
        HttpResponseMessage partyResponse = await client2.SendAsync(partyReqMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, partyResponse.StatusCode);

        RequestSystemResponse? requestGet = JsonSerializer.Deserialize<RequestSystemResponse>(await partyResponse.Content.ReadAsStringAsync());

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.BadRequest, approveResponseMessage.StatusCode);
        ProblemDetails problem = await approveResponseMessage.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The Delegation failed.", problem.Detail);
    }

    [Fact]
    public async Task Reject_Request_By_RequestId_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string partyEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}";

        HttpRequestMessage partyReqMessage = new(HttpMethod.Get, partyEndpoint);
        HttpResponseMessage partyResponse = await client2.SendAsync(partyReqMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, partyResponse.StatusCode);

        RequestSystemResponse? requestGet = JsonSerializer.Deserialize<RequestSystemResponse>(await partyResponse.Content.ReadAsStringAsync());

        string rejectEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/reject";
        HttpRequestMessage rejectRequestMessage = new(HttpMethod.Post, rejectEndpoint);
        HttpResponseMessage rejectResponseMessage = await client2.SendAsync(rejectRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, rejectResponseMessage.StatusCode);
    }

    [Fact]
    public async Task Get_All_Requests_By_System_Paginated_Single()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);
        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Get the Request
        HttpClient client2 = CreateClient();
        string token2 = AddSystemUserRequesReadTestTokenToClient(client2);
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/bysystem/{req.SystemId}";

        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        Paginated<RequestSystemResponse>? res2 = await message2.Content.ReadFromJsonAsync<Paginated<RequestSystemResponse>>();
        Assert.True(res2 is not null);
        var list = res2.Items.ToList();
        Assert.NotEmpty(list);
        Assert.Equal(req.SystemId, list[0].SystemId);
    }

    [Fact]
    public async Task Get_All_Requests_By_System_Paginated_Several()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);

        // Arrange
        string systemId = "991825827_the_matrix";
        
        await CreateSeveralRequest(client, _paginationSize, systemId);

        // Get the Request
        HttpClient client2 = CreateClient();
        string token2 = AddSystemUserRequesReadTestTokenToClient(client2);
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/bysystem/{systemId}";

        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        Paginated<RequestSystemResponse>? res2 = await message2.Content.ReadFromJsonAsync<Paginated<RequestSystemResponse>>();
        Assert.True(res2 is not null);
        var list = res2.Items.ToList();
        Assert.NotEmpty(list);
        Assert.Equal(_paginationSize, list.Count);
        Assert.Contains(list, x => x.PartyOrgNo == "910493353");
        Assert.NotNull(res2.Links.Next);

        HttpResponseMessage message3 = await client2.GetAsync(res2.Links.Next);
        Assert.Equal(HttpStatusCode.OK, message3.StatusCode);
        Paginated<RequestSystemResponse>? res3 = await message3.Content.ReadFromJsonAsync<Paginated<RequestSystemResponse>>();
        Assert.True(res3 is not null);
    }

    [Fact]
    public async Task Delete_Request_ByGuid_Accepted()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Get by Guid
        Guid testId = res.Id;
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";
        HttpClient client2 = CreateClient();
        string token2 = AddSystemUserRequesReadTestTokenToClient(client2);
        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);        
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        RequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.True(res2 is not null);
        Assert.Equal(testId, res2.Id);

        // Delete by Guid
        string endpoint3 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";
        HttpResponseMessage message3 = await client.DeleteAsync(endpoint3);
        string debug = "pause_here";
        Assert.Equal(HttpStatusCode.Accepted, message3.StatusCode);

        // Get by Guid after delete return NotFound
        HttpResponseMessage message4 = await client2.GetAsync(endpoint2);
        Assert.Equal(HttpStatusCode.NotFound, message4.StatusCode);
    }

    [Fact]
    public async Task Delete_Request_ByGuid_Forbid()
    {
        HttpClient client = CreateClient();
        string token = AddTestTokenToClient(client);

        Guid testId = Guid.NewGuid();

        //Delete by Guid
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor/{testId}";
        HttpResponseMessage message = await client.DeleteAsync(endpoint);
        string debug = "pause_here";
        Assert.Equal(HttpStatusCode.Forbidden, message.StatusCode);
    }

    [Fact]
    public async Task Delete_Request_ByGuid_NotFound()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        // Get by Guid
        Guid testId = res.Id;
        string endpoint2 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";
        HttpClient client2 = CreateClient();
        string token2 = AddSystemUserRequesReadTestTokenToClient(client2);
        HttpResponseMessage message2 = await client2.GetAsync(endpoint2);
        Assert.Equal(HttpStatusCode.OK, message2.StatusCode);
        RequestSystemResponse? res2 = await message2.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.True(res2 is not null);
        Assert.Equal(testId, res2.Id);

        // Delete by Guid
        string endpoint3 = $"/authentication/api/v1/systemuser/request/vendor/{testId}";
        HttpResponseMessage message3 = await client.DeleteAsync(endpoint3);
        Assert.Equal(HttpStatusCode.Accepted, message3.StatusCode);

        // Delete by Guid Again
        HttpResponseMessage message4 = await client.DeleteAsync(endpoint3);
        Assert.Equal(HttpStatusCode.NotFound, message4.StatusCode);
    }

    [Fact]
    public async Task Approve_Request_SubsetOfSeveralRights_Success()
    {
        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister2Rights.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);
        string endpoint = $"/authentication/api/v1/systemuser/request/vendor";

        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:resource",
                    Value = "ske-krav-og-betalinger"
                }
            ]
        };

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);

        //// Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);
    }

    private static async Task CreateSeveralRequest(HttpClient client, int paginationSize, string systemId)
    {
        var tasks = Enumerable.Range(0, paginationSize + 1)
                              .Select(i => CreateRequest(client, i, systemId))
                              .ToList();

        await Task.WhenAll(tasks);
    }

    private static async Task CreateRequest(HttpClient client, int externalRef, string systemId)
    {
        Right right = new()
        {
            Resource =
            [
                new AttributePair()
                        {
                            Id = "urn:altinn:resource",
                            Value = "ske-krav-og-betalinger"
                        }
            ]
        };

        CreateRequestSystemUser req = new()
        {
            ExternalRef = externalRef.ToString(),
            SystemId = systemId,
            PartyOrgNo = "910493353",
            Rights = [right]
        }; 

        HttpRequestMessage request = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/request/vendor")
        {
            Content = JsonContent.Create(req)
        };
        HttpResponseMessage message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);
        RequestSystemResponse? res = await message.Content.ReadFromJsonAsync<RequestSystemResponse>();
        Assert.NotNull(res);
        Assert.Equal(req.ExternalRef, res.ExternalRef);
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
        var res = await client.PostAsync(token, $"/authentication/api/v1/systemregister/vendor/", content);
        return res;
    }

    private static string AddTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.write", prefixes);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }

    private static string AddSystemUserRequestWriteTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.write", prefixes);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }

    private static string AddSystemUserRequesReadTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.read", prefixes);
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

        HttpRequestMessage request = new(HttpMethod.Post, $"/authentication/api/v1/systemregister/vendor/");
        request.Content = content;
        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response;
    }
}
