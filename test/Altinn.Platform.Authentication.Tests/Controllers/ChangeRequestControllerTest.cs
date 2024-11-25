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
using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
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
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;
#nullable enable
/// <summary>
/// Test class for the ChangeRequestController
/// </summary>
public class ChangeRequestControllerTest(
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
    private readonly Mock<IPDP> _pdpMock = new();
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
        services.AddSingleton(_pdpMock.Object);
        services.AddSingleton<IPartiesClient, PartiesClientMock>();
        services.AddSingleton<ISystemUserService, SystemUserServiceMock>();
        services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
        services.AddSingleton<IRequestSystemUser, RequestSystemUserService>();
        services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
        services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
        services.AddSingleton<IRequestRepository, RequestRepository>();
        services.AddSingleton<ISystemUserRepository, SystemUserRepository>();
        services.AddSingleton<IChangeRequestRepository, ChangeRequestRepository>();
        services.AddSingleton<IChangeRequestSystemUser, ChangeRequestSystemUserService>();
        SetupDateTimeMock();
        SetupGuidMock();
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Create_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Change Request, create
        string verifyChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, verifyChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Approve_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        Right right2 = new()
        {
            Resource =
            [
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

        // Party Get Request
        HttpClient client2 = CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right2],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Approve the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}/approve";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Post, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client3.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveChangeResponseMessage.StatusCode);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Reject_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}/reject";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Post, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client3.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveChangeResponseMessage.StatusCode);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Delete_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string requestId = createdResponse.Id.ToString();

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/{requestId}";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Delete, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Accepted, approveChangeResponseMessage.StatusCode);
    }

    private static bool DeepCompare(List<Right> requiredRights1, List<Right> requiredRights2)
    {
        bool result = true;

        if (requiredRights1.Count != requiredRights2.Count)
        {
            return false;
        }               

        foreach (Right right in requiredRights1) 
        {
            foreach (AttributePair pair in right.Resource)
            {
                if (!DeepFind(requiredRights2, pair.Value))
                {                    
                    return false;
                }
            }
        }

        return result;
    }

    private static bool DeepFind(List<Right> requiredRights, string value)
    {
        foreach (Right right in requiredRights)
        {
            foreach (AttributePair pair in right.Resource)
            {
                if (pair.Value == value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public async Task VerifyChangeRequest_AllRightsPermit_ReturnEmptySet()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Change Request, verify all rights permit
        string verifyChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/verify/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, verifyChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage verifyResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, verifyResponseMessage.StatusCode);

        ChangeRequestResponse? verifyResponse = await verifyResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(verifyResponse);
        Assert.Empty(verifyResponse.RequiredRights);
    }

    [Fact]
    public async Task VerifyChangeRequest_NotAllRightsPermit_ReturnSet()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Change Request, verify all rights permit
        string verifyChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/verify/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, verifyChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage verifyResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, verifyResponseMessage.StatusCode);

        ChangeRequestResponse? verifyResponse = await verifyResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(verifyResponse);
        Assert.NotEmpty(verifyResponse.RequiredRights);
        Assert.True(DeepCompare(verifyResponse.RequiredRights, change.RequiredRights));
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_GetStatusByGuid_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []            
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        string token3 = AddSystemUserRequestReadTestTokenToClient(client3);
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/{requestId}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client3.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponse statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_GetStatusByExternalIds_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string systemId = change.SystemId;
        string orgNo = change.PartyOrgNo;
        string externalRef = change.ExternalRef;
        HttpClient client3 = CreateClient();
        string token3 = AddSystemUserRequestReadTestTokenToClient(client3);
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/byexternalref/{systemId}/{orgNo}/{externalRef}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client3.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponse statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_FrontEnd_GetRedirect_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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
            Rights = [right],
            RedirectUrl = "https://altinn.no"
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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = [],
            RedirectUrl = "https://altinn.no"
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/redirect/{requestId}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client3.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);

        Assert.NotNull(statusChangeResponseMessage.Content);
        RedirectUrl redirectUrlDTO = await statusChangeResponseMessage.Content.ReadFromJsonAsync<RedirectUrl>();
        Assert.NotNull(redirectUrlDTO);
        Assert.Equal(change.RedirectUrl, redirectUrlDTO.Url);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_GetStatusFromFrontend_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultList();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = "external",
            SystemId = "991825827_the_matrix",
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Reject the Change Request
        string requestId = createdResponse.Id.ToString();
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client2.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponse statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
    }

    [Fact]
    public async Task Get_All_ChangeRequests_By_System_Paginated_Several()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
        HttpResponseMessage response = await CreateSystemRegister(dataFileName);

        HttpClient client = CreateClient();
        string token = AddSystemUserRequestWriteTestTokenToClient(client);

        // Arrange
        string systemId = "991825827_the_matrix";

        await CreateSeveralChangeRequest(_paginationSize, systemId);

        // Get the Request
        HttpClient client2 = CreateClient();
        string token2 = AddSystemUserRequestReadTestTokenToClient(client2);
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

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        HttpResponseMessage message3 = await client2.GetAsync(res2.Links.Next);
        Assert.Equal(HttpStatusCode.OK, message3.StatusCode);
        Paginated<RequestSystemResponse>? res3 = await message3.Content.ReadFromJsonAsync<Paginated<RequestSystemResponse>>();
        Assert.True(res3 is not null);
    }

    private async Task CreateSeveralChangeRequest(int paginationSize, string systemId)
    {
        var tasks = Enumerable.Range(0, paginationSize + 1)
                              .Select(i => CreateChangeRequest(i, systemId))
                              .ToList();

        await Task.WhenAll(tasks);
    }

    private async Task CreateChangeRequest(int externalRef, string systemId)
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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
            ExternalRef = externalRef.ToString(),
            SystemId = systemId,
            PartyOrgNo = "910493353",
            Rights = [right]
        };

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

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

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/";

        ChangeRequestSystemUser change = new()
        {
            ExternalRef = externalRef.ToString(),
            SystemId = systemId,
            PartyOrgNo = "910493353",
            RequiredRights = [right],
            UnwantedRights = []
        };

        HttpRequestMessage createChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(createChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));
    }

    private static List<XacmlJsonResult> GetDecisionResultListNotAllPermit()
    {
        return
        [
            new XacmlJsonResult
            {
                Decision = XacmlContextDecision.Permit.ToString(),
                Category =
                [
                    new XacmlJsonCategory
                    {
                        Id = "urn:altinn:resource:resourceparty",
                        Attribute =
                        [
                            new XacmlJsonAttribute
                            {
                                AttributeId = "urn:altinn:resource:resourceparty:partyid",
                                Value = "500000"
                            }
                        ]
                    }
                ]
            },
            new XacmlJsonResult
            {
                Decision = XacmlContextDecision.Deny.ToString(),
                Category =
                [
                    new XacmlJsonCategory
                    {
                        Id = "urn:altinn:resource:resourceparty",
                        Attribute =
                        [
                            new XacmlJsonAttribute
                            {
                                AttributeId = "urn:altinn:resource:resourceparty:partyid",
                                Value = "500000"
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static List<XacmlJsonResult> GetDecisionResultList()
    {
        return new()
        {
            new XacmlJsonResult
            {
                Decision = XacmlContextDecision.Permit.ToString(),
                Category = new List<XacmlJsonCategory>
                {
                    new XacmlJsonCategory
                    {
                        Id = "urn:altinn:resource:resourceparty",
                        Attribute = new List<XacmlJsonAttribute>
                        {
                            new XacmlJsonAttribute
                            {
                                AttributeId = "urn:altinn:resource:resourceparty:partyid",
                                Value = "500000"
                            }
                        }
                    }
                }
            },
            new XacmlJsonResult
            {
                Decision = XacmlContextDecision.Permit.ToString(),
                Category = new List<XacmlJsonCategory>
                {
                    new XacmlJsonCategory
                    {
                        Id = "urn:altinn:resource:resourceparty",
                        Attribute = new List<XacmlJsonAttribute>
                        {
                            new XacmlJsonAttribute
                            {
                                AttributeId = "urn:altinn:resource:resourceparty:partyid",
                                Value = "500000"
                            }
                        }
                    }
                }
            }
        };
    }

    private static List<XacmlJsonResult> GetDecisionResultSingle()
    {
        return new()
        {
            new XacmlJsonResult
            {
                Decision = XacmlContextDecision.Permit.ToString(),
                Category = new List<XacmlJsonCategory>
                {
                    new XacmlJsonCategory
                    {
                        Id = "urn:altinn:resource:resourceparty",
                        Attribute = new List<XacmlJsonAttribute>
                        {
                            new XacmlJsonAttribute
                            {
                                AttributeId = "urn:altinn:resource:resourceparty:partyid",
                                Value = "500000"
                            }
                        }
                    }
                }
            }
        };
    }

    private static string GetConfigPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, $"../../../appsettings.json");
    }

    private void SetupDateTimeMock()
    {
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
    }

    private void SetupGuidMock()
    {
        guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
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

    private static string AddSystemUserRequestWriteTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.write", prefixes);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }

    private static string AddSystemUserRequestReadTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.read", prefixes);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }
}
