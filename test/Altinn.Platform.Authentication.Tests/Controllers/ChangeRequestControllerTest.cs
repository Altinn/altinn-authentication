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
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
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
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using static System.Net.Mime.MediaTypeNames;

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
    private static readonly DateTimeOffset TestTime = new(2025, 05, 15, 02, 05, 00, TimeSpan.Zero);
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
        services.AddSingleton<ISystemUserService, SystemUserService>();
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

        string systemId = "991825827_the_matrix";

        // Arrange
        CreateRequestSystemUser req = new()
        {
            ExternalRef = "external",
            SystemId = systemId,
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={systemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);
        Assert.True(systemuser.Rights?.Count > 0);
        Assert.True(systemuser.AccessPackages?.Count > 0);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // Change Request, create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = []
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.Contains("&DONTCHOOSEREPORTEE=true", createdResponse.ConfirmUrl);
        Assert.NotNull(createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest
    /// </summary>
    [Fact]
    public async Task ChangeRequest_With_AccessPackages_Create_ReturnOk()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister2RightsAndAP.json";
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

        AccessPackage accessPackage = new()
        {
            Urn = "urn:altinn:accesspackage:skatt-naering"
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // Change Request, create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = [right],
            RequiredAccessPackages = [accessPackage]            
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.Contains("&DONTCHOOSEREPORTEE=true", createdResponse.ConfirmUrl);
        Assert.NotNull(createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));
        Guid systemUserIdFromChangeRequest = createdResponse.SystemUserId;
                
        // Get and Approve the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, now: TestTime));

        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        string getChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}";
        HttpRequestMessage getChangeRequestMessage = new(HttpMethod.Get, getChangeRequestEndpoint);
        HttpResponseMessage getChangeResponseMessage = await client3.SendAsync(getChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getChangeResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}/approve";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Post, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client3.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveChangeResponseMessage.StatusCode);

        // Doublecheck that the correct SystemUser was updated
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        HttpClient client4 = CreateClient();
        client4.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, now: TestTime));

        string getSystemUserEndpoint = $"/authentication/api/v1/systemuser/{partyId}/{systemUserIdFromChangeRequest}";
        HttpRequestMessage getSystemUserRequestMessage = new(HttpMethod.Get, getSystemUserEndpoint);
        HttpResponseMessage getSystemUserResponseMessage = await client4.SendAsync(getSystemUserRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getSystemUserResponseMessage.StatusCode);
        Assert.NotNull(getSystemUserResponseMessage.Content);
        SystemUserDetailExternalDTO? systemUser = await getSystemUserResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();
        Assert.NotNull(systemUser);
        Assert.Equal(systemUserIdFromChangeRequest, Guid.Parse(systemUser.Id));
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest
    /// </summary>
    [Fact]
    public async Task ChangeRequest_With_AccessPackages_Create_Return_BadRequest()
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

        AccessPackage accessPackage = new()
        {
            Urn = "urn:altinn:accesspackage:skatt-naering"
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

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

        // Change Request, create
        string verifyChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?orgno=910493353&external-ref=external&system-id=991825827_the_matrix";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = [right],
            RequiredAccessPackages = [accessPackage]
        };

        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, verifyChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };
        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.BadRequest, createdResponseMessage.StatusCode);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Create_SecondAttempt_AlsoReturnOk()
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // Change Request, create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = []
        };

        // First attempt return Created
        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };

        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.NotNull(createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        // Second attempt return OK (as the ChangeRequest already exists)
        HttpRequestMessage verifyChangeRequestMessage2 = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };

        HttpResponseMessage createdResponseMessage2 = await client.SendAsync(verifyChangeRequestMessage2, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, createdResponseMessage2.StatusCode);

        ChangeRequestResponse? createdResponse2 = await createdResponseMessage2.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse2);
        Assert.NotEmpty(createdResponse2.RequiredRights);
        Assert.Contains("&DONTCHOOSEREPORTEE=true", createdResponse2.ConfirmUrl);
        Assert.Equal(createdResponse2.ConfirmUrl, createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse2.RequiredRights, change.RequiredRights));
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Create_Another_Return_Created()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister2RightsAndAP.json";
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // Change Request, create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = []
        };

        // First attempt return Created
        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };

        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.NotNull(createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        Guid id2 = Guid.NewGuid();
        string createChangeRequestEndpoint2 = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id2}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change2 = new()
        {
            RequiredRights = [],
            UnwantedRights = [],
            RequiredAccessPackages = [
                new()
                {
                    Urn = "urn:altinn:accesspackage:skatt-naering"
                }
            ]
        };

        // Second attempt return Create (as the ChangeRequest has a new Correllation Id)
        HttpRequestMessage verifyChangeRequestMessage2 = new(HttpMethod.Post, createChangeRequestEndpoint2)
        {
            Content = JsonContent.Create(change2)
        };

        HttpResponseMessage createdResponseMessage2 = await client.SendAsync(verifyChangeRequestMessage2, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage2.StatusCode);

        ChangeRequestResponse? createdResponse2 = await createdResponseMessage2.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse2);
        Assert.NotEmpty(createdResponse2.RequiredAccessPackages);
        Assert.Contains("&DONTCHOOSEREPORTEE=true", createdResponse2.ConfirmUrl);
        Assert.NotEqual(createdResponse2.ConfirmUrl, createdResponse.ConfirmUrl);        
    }

    /// <summary>
    /// Even with a new set of required rights, when using the same Correllation id, the previous Request is returned OK
    /// </summary>
    [Fact]
    public async Task ChangeRequest_Create_Another_Return_Badrequest()
    {
        List<XacmlJsonResult> xacmlJsonResults = GetDecisionResultSingle();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Create System used for test
        string dataFileName = "Data/SystemRegister/Json/SystemRegister2RightsAndAP.json";
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // Change Request, create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
            RequiredRights = [right2],
            UnwantedRights = []
        };

        // First attempt return Created
        HttpRequestMessage verifyChangeRequestMessage = new(HttpMethod.Post, createChangeRequestEndpoint)
        {
            Content = JsonContent.Create(change)
        };

        HttpResponseMessage createdResponseMessage = await client.SendAsync(verifyChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Created, createdResponseMessage.StatusCode);

        ChangeRequestResponse? createdResponse = await createdResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(createdResponse);
        Assert.NotEmpty(createdResponse.RequiredRights);
        Assert.NotNull(createdResponse.ConfirmUrl);
        Assert.True(DeepCompare(createdResponse.RequiredRights, change.RequiredRights));

        Guid id2 = Guid.NewGuid();
        string createChangeRequestEndpoint2 = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change2 = new()
        {
            RequiredRights = [],
            UnwantedRights = [],
            RequiredAccessPackages = [
                new()
                {
                    Urn = "urn:altinn:accesspackage:skatt-naering"
                }
            ]
        };

        // Second attempt return Create (as the ChangeRequest has a new Correllation Id)
        HttpRequestMessage verifyChangeRequestMessage2 = new(HttpMethod.Post, createChangeRequestEndpoint2)
        {
            Content = JsonContent.Create(change2)
        };

        HttpResponseMessage createdResponseMessage2 = await client.SendAsync(verifyChangeRequestMessage2, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, createdResponseMessage2.StatusCode);
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage);
        ////string errorContent = await getResponseMessage.Content.ReadAsStringAsync();
        //Console.WriteLine(errorContent);
        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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
        Assert.NotEqual(Guid.Empty, createdResponse.Id);
        Guid systemUserIdFromChangeRequest = createdResponse.SystemUserId;

        // works up to here
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        // Approve the Change Request
        string requestId = createdResponse.Id.ToString();
        HttpClient client3 = CreateClient();
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, now: TestTime));

        string getChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}";
        HttpRequestMessage getChangeRequestMessage = new(HttpMethod.Get, getChangeRequestEndpoint);
        HttpResponseMessage getChangeResponseMessage = await client3.SendAsync(getChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getChangeResponseMessage.StatusCode);

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}/approve";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Post, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client3.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveChangeResponseMessage.StatusCode);

        // Doublecheck that the correct SystemUser was updated
        xacmlJsonResults = GetDecisionResultSingle();
        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        HttpClient client4 = CreateClient();
        client4.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, now: TestTime));

        string getSystemUserEndpoint = $"/authentication/api/v1/systemuser/{partyId}/{systemUserIdFromChangeRequest}";
        HttpRequestMessage getSystemUserRequestMessage = new(HttpMethod.Get, getSystemUserEndpoint);
        HttpResponseMessage getSystemUserResponseMessage = await client4.SendAsync(getSystemUserRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getSystemUserResponseMessage.StatusCode);
        Assert.NotNull(getSystemUserResponseMessage.Content);
        SystemUserDetailInternalDTO? systemUser = await getSystemUserResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailInternalDTO>();
        Assert.NotNull(systemUser);
        Assert.Equal(systemUserIdFromChangeRequest, Guid.Parse(systemUser.Id));
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
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, now: TestTime));

        string approveChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}/reject";
        HttpRequestMessage approveChangeRequestMessage = new(HttpMethod.Post, approveChangeRequestEndpoint);
        HttpResponseMessage approveChangeResponseMessage = await client3.SendAsync(approveChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveChangeResponseMessage.StatusCode);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then delete it
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
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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

    [Fact (Skip = "deprecated")]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

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

    [Fact (Skip = "deprecated")]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

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
            Rights = [right2],
            AccessPackages = []
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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
        ChangeRequestResponse? statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
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
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string systemId = "991825827_the_matrix";
        string orgNo = "910493353";
        string externalRef = "external";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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
        HttpClient client3 = CreateClient();
        string token3 = AddSystemUserRequestReadTestTokenToClient(client3);
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor/byexternalref/{systemId}/{orgNo}/{externalRef}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client3.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponse? statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
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
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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

        // Get the change request
        string requestId = createdResponse.Id.ToString();
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{partyId}/{requestId}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client2.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponse? statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponse>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
    }

    /// <summary>
    /// After having verified that the ChangeRequest is needed, create a ChangeRequest, then approve it
    /// </summary>
    [Fact]
    public async Task ChangeRequest_GetStatusFromFrontend_ReturnOk_WithoutPartyId()
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
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

        int partyId = 500000;

        // Approve the SystemUser
        string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
        HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
        HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string ext = "external";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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

        // Get the change request
        string requestId = createdResponse.Id.ToString();
        string statusChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/{requestId}";
        HttpRequestMessage statusChangeRequestMessage = new(HttpMethod.Get, statusChangeRequestEndpoint);
        HttpResponseMessage statusChangeResponseMessage = await client2.SendAsync(statusChangeRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, statusChangeResponseMessage.StatusCode);
        Assert.NotNull(statusChangeResponseMessage.Content);
        ChangeRequestResponseInternal? statusResponse = await statusChangeResponseMessage.Content.ReadFromJsonAsync<ChangeRequestResponseInternal>();
        Assert.NotNull(statusResponse);
        Assert.Equal(ChangeRequestStatus.New.ToString(), statusResponse.Status);
        Assert.Equal(partyId, statusResponse.PartyId);
        Assert.Equal(new Guid("00000000-0000-0000-0005-000000000000"), statusResponse.PartyUuid);
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
        string dataFileName = "Data/SystemRegister/Json/SystemRegister2Rights.json";
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
        var tasks = Enumerable.Range(0, paginationSize +1)
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
            ExternalRef = externalRef.ToString(),
            SystemId = systemId,
            PartyOrgNo = "910493353",
            Rights = [right2]
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
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true, now: TestTime));

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

        string getEndpoint = $"/authentication/api/v1/systemuser/vendor/byquery?system-id={req.SystemId}&orgno={req.PartyOrgNo}&external-ref={req.ExternalRef}";
        HttpRequestMessage getRequestMessage = new(HttpMethod.Get, getEndpoint);
        HttpResponseMessage getResponseMessage = await client.SendAsync(getRequestMessage, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, getResponseMessage.StatusCode);

        SystemUserDetailExternalDTO? systemuser = await getResponseMessage.Content.ReadFromJsonAsync<SystemUserDetailExternalDTO>();

        Assert.NotNull(systemuser);

        xacmlJsonResults = GetDecisionResultListNotAllPermit();

        _pdpMock.Setup(p => p.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>())).ReturnsAsync(new XacmlJsonResponse
        {
            Response = xacmlJsonResults
        });

        Guid id = Guid.NewGuid();
        string orgno = "910493353";
        string sys = "991825827_the_matrix";

        // ChangeRequest create
        string createChangeRequestEndpoint = $"/authentication/api/v1/systemuser/changerequest/vendor?correlation-id={id}&system-user-id={systemuser.Id}";

        ChangeRequestSystemUser change = new()
        {
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
        string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder!, $"../../../appsettings.json");
    }

    private void SetupDateTimeMock()
    {
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(TestTime);
    }

    private void SetupGuidMock()
    {
        guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
    }

    private async Task<HttpResponseMessage> CreateSystemRegister(string dataFileName)
    {
        HttpClient client = CreateClient();
        string[] prefixes = { "altinn", "digdir" };
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes, TestTime);
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
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.write", prefixes, TestTime);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }

    private static string AddSystemUserRequestReadTestTokenToClient(HttpClient client)
    {
        string[] prefixes = ["altinn", "digdir"];
        string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.read", prefixes, TestTime);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return token;
    }
}
