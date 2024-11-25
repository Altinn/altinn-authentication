#nullable enable
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
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Model;
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
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Unit Tests for the SystemUnitController
    /// </summary>
    public class SystemUserControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private readonly Mock<IUserProfileService> _userProfileService = new Mock<IUserProfileService>();
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new Mock<ISblCookieDecryptionService>();
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

        private readonly Mock<TimeProvider> timeProviderMock = new Mock<TimeProvider>();
        private readonly Mock<IGuidService> guidService = new Mock<IGuidService>();
        private readonly Mock<IEventsQueueClient> _eventQueue = new Mock<IEventsQueueClient>();

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
            services.AddSingleton<IPDP, PepWithPDPAuthorizationMock>();
            services.AddSingleton<IPartiesClient, PartiesClientMock>();
            services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
            services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
            SetupDateTimeMock();
            SetupGuidMock();
        }
    
        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsListOK()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage listSystemUserResponse = await client.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
            List<SystemUser>? list = JsonSerializer.Deserialize<List<SystemUser>>(await listSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 1);
        }

        /// <summary>
        /// Scenario where user is not authorized to view list of system users
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsForbidden()
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500801;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);
        }

        /// <summary>
        /// Scenario where user does not have a valid token
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsUnathorized()
        {
            HttpClient client = CreateClient();
           
            int partyId = 500801;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);
        }

        /// <summary>
        /// Scenario where user does not have a valid token
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsNotFound()
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsOK()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));
            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo=991825827&systemUserOwnerOrgNo=910493353&clientId=32ef65ac-6e62-498d-880f-76c85c2052ae");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp_WrongScope()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.wrooong", null));
            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo=991825827&systemUserOwnerOrgNo=910493353&clientId=32ef65ac-6e62-498d-880f-76c85c2052ae");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
      
            Assert.Equal(HttpStatusCode.Forbidden, lookupSystemUserResponse.StatusCode);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsNotFound()
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}/{id}");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.NotFound, lookupSystemUserResponse.StatusCode);

        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            _ = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;

            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Created, createSystemUserResponse.StatusCode);
            SystemUser? shouldBeCreated = await createSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();
            Assert.NotNull(shouldBeCreated);

            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = await lookupSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.NotNull(systemUserDoesExist);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);

            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser? shouldBeDeleted = await response3.Content.ReadFromJsonAsync<SystemUser>();            
            Assert.Equal(HttpStatusCode.NotFound, response3.StatusCode);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsNotFound()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            _ = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;

            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{partyId}/{Guid.NewGuid()}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
        }

        [Fact(Skip = "Leveranse 3")]
        public async Task SystemUser_Update_IntegrationTitle_ReturnsOk()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response2 = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await response2.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            SystemUserUpdateDto updateDto = new SystemUserUpdateDto
            {
                IntegrationTitle = "updated_integration_title",
                Id = shouldBeCreated.Id,
                PartyId = shouldBeCreated.PartyId,
                SystemId = shouldBeCreated.SystemId
            };

            HttpRequestMessage updateRequest = new(HttpMethod.Put, $"/authentication/api/v1/systemuser/{updateDto.Id}");
            updateRequest.Content = JsonContent.Create<SystemUserUpdateDto>(updateDto, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage updateResponse = await client.SendAsync(updateRequest, HttpCompletionOption.ResponseContentRead);
            
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeUpdated = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), _options)!;

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.Equal("updated_integration_title", shouldBeUpdated!.IntegrationTitle);
        }

        [Fact(Skip = "Leveranse 3")]
        public async Task SystemUser_Update_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            SystemUser doesNotExist = new() { Id = "123" };

            HttpRequestMessage request2 = new(HttpMethod.Put, $"/authentication/api/v1/systemuser/")
            {
                Content = JsonContent.Create<SystemUser>(doesNotExist, new MediaTypeHeaderValue("application/json"), _options)
            };
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.False(response2.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",  PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
                         
            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.Created, createSystemUserResponse.StatusCode);
            Assert.NotNull(shouldBeCreated);
            Assert.Equal("IntegrationTitleValue", shouldBeCreated.IntegrationTitle);            
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsForbidden()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500801;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
         
            Assert.Equal(HttpStatusCode.Forbidden, createSystemUserResponse.StatusCode);
        }

        [Fact]
        public async Task SystemUser_ListByVendorsSystem_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.Created, createSystemUserResponse.StatusCode);
            Assert.NotNull(shouldBeCreated);
            Assert.Equal("IntegrationTitleValue", shouldBeCreated.IntegrationTitle);

            HttpClient vendorClient = CreateClient();
            string[] prefixes = { "altinn", "digdir" };
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
            vendorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            string vendorEndpoint = $"/authentication/api/v1/systemuser/vendor/bysystem/{newSystemUser.SystemId}";

            HttpRequestMessage vendorMessage = new(HttpMethod.Get, vendorEndpoint);
            HttpResponseMessage vendorResponse = await vendorClient.SendAsync(vendorMessage, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.OK, vendorResponse.StatusCode);

            var result = await vendorResponse.Content.ReadFromJsonAsync<Paginated<SystemUser>>();
            Assert.NotNull(result);
            var list = result.Items.ToList();
            
            Assert.NotNull(list);
            Assert.NotEmpty(list);
            Assert.Equal(list[0].IntegrationTitle, newSystemUser.IntegrationTitle);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
  
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/bff")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<CreateSystemUserResponse>();
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);
            SystemUser? shouldBeCreated = result?.SystemUser;
            Assert.NotNull(shouldBeCreated);
            Assert.Equal(newSystemUser.SystemId, shouldBeCreated.SystemId);
            Assert.Equal(newSystemUser.IntegrationTitle, shouldBeCreated.IntegrationTitle);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_Returns_DelegationError()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500001;

            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/bff")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<CreateSystemUserResponse>();
            Assert.Equal(HttpStatusCode.BadRequest, createSystemUserResponse.StatusCode);
            Assert.True(result.IsSuccess = false);
            Assert.Null(result.SystemUser);
            Assert.NotEmpty(result.Problem?.Detail);
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SystemUserControllerTest).Assembly.Location).LocalPath);
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

    }
}
