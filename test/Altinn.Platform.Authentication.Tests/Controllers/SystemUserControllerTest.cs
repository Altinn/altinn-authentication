﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Drawing.Text;
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
using Altinn.Authentication.Core.Problems;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
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

        private int _paginationSize = 10;

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

            // _paginationSize = configuration.GetValue<int>("PaginationOptions:Size");
            services.AddSingleton(_eventQueue.Object);
            services.AddSingleton(timeProviderMock.Object);
            services.AddSingleton(guidService.Object);
            services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
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
            var list = await response.Content.ReadFromJsonAsync<List<SystemUser>>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(list);
            Assert.Empty(list);
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));

            string clientId = "32ef65ac-6e62-498d-880f-76c85c2052ae";
            string systemProviderOrgNo = "991825827";
            string systemUserOwnerOrgNo = "910493353";
            string externalRef = "910493353";

            HttpRequestMessage looukpSystemUserRequest = 
                new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}&clientId={clientId}&externalRef={externalRef}");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp_Deleted_SystemUser_ReturnNotFound()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            HttpClient client2 = CreateClient();
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));

            string clientId = "32ef65ac-6e62-498d-880f-76c85c2052ae";
            string systemProviderOrgNo = "991825827";
            string systemUserOwnerOrgNo = "910493353";
            string externalRef = "910493353";

            HttpRequestMessage looukpSystemUserRequest =
                new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}&clientId={clientId}&externalRef={externalRef}");
            HttpResponseMessage lookupSystemUserResponse = await client2.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);
                      
            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);

            HttpRequestMessage looukpSystemUserRequest2 =
                new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}&clientId={clientId}&externalRef={externalRef}");
            HttpResponseMessage lookupSystemUserResponse2 = await client2.SendAsync(looukpSystemUserRequest2, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist2 = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse2.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.OK, lookupSystemUserResponse2.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, lookupSystemUserResponse2.StatusCode);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp_Deleted_SystemUser_ReturnNotFound_CreateNewWorks()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            HttpClient client2 = CreateClient();
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));

            string clientId = "32ef65ac-6e62-498d-880f-76c85c2052ae";
            string systemProviderOrgNo = "991825827";
            string systemUserOwnerOrgNo = "910493353";
            string externalRef = "910493353";

            HttpRequestMessage looukpSystemUserRequest =
                new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}&clientId={clientId}&externalRef={externalRef}");
            HttpResponseMessage lookupSystemUserResponse = await client2.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);

            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{partyId}/{shouldBeCreated.Id}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);

            HttpRequestMessage looukpSystemUserRequest2 =
                new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo={systemProviderOrgNo}&systemUserOwnerOrgNo={systemUserOwnerOrgNo}&clientId={clientId}&externalRef={externalRef}");
            HttpResponseMessage lookupSystemUserResponse2 = await client2.SendAsync(looukpSystemUserRequest2, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.OK, lookupSystemUserResponse2.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, lookupSystemUserResponse2.StatusCode);

            // Create new systemuser right after the delete, before the request has been archived, will fail if the request is not also deleted
            HttpRequestMessage createSystemUserRequest2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse2 = await client.SendAsync(createSystemUserRequest2, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated2 = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse2.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated2);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp_withExternalRef_ReturnOK()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));
            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo=991825827&systemUserOwnerOrgNo=910493353&clientId=32ef65ac-6e62-498d-880f-76c85c2052ae&externalRef=910493353");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            SystemUser? systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await lookupSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, lookupSystemUserResponse.StatusCode);
            Assert.True(systemUserDoesExist is not null);
            Assert.Equal(shouldBeCreated.Id, systemUserDoesExist.Id);
        }

        [Fact]
        public async Task SystemUser_Get_byExternalIdp_withWrongExternalRef()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            Assert.NotNull(shouldBeCreated);

            // Replaces token with Maskinporten token faking
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:maskinporten/systemuser.read", null));
            HttpRequestMessage looukpSystemUserRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/byExternalId?systemProviderOrgNo=991825827&systemUserOwnerOrgNo=910493353&clientId=32ef65ac-6e62-498d-880f-76c85c2052ae&externalRef=tjobing");
            HttpResponseMessage lookupSystemUserResponse = await client.SendAsync(looukpSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, lookupSystemUserResponse.StatusCode);
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);
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

        [Fact]
        public async Task SystemUser_CreateAndDelegate_Returns_DelegationErrorDetail()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500004;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);
            
            Assert.Equal(HttpStatusCode.Forbidden, createSystemUserResponse.StatusCode);
            Assert.Equal(Problem.UnableToDoDelegationCheck.Detail, problemDetails?.Detail);
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create");
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);
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

            Paginated<SystemUser>? result = await vendorResponse.Content.ReadFromJsonAsync<Paginated<SystemUser>>();
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);           
            
            Assert.Equal(newSystemUser.IntegrationTitle, result?.IntegrationTitle);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_Double_ReturnsBadRequest()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);

            Assert.Equal(newSystemUser.IntegrationTitle, result?.IntegrationTitle);

            HttpRequestMessage createSystemUserRequest2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse2 = await client.SendAsync(createSystemUserRequest2, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, createSystemUserResponse2.StatusCode);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_2Rights_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister2Rights.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;

            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);

            Assert.Equal(newSystemUser.IntegrationTitle, result?.IntegrationTitle);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_2RightsSubresource_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister2RightsSubResource.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;

            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = "991825827_the_matrix",
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            var result = await createSystemUserResponse.Content.ReadFromJsonAsync<SystemUser>();
            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);

            Assert.Equal(newSystemUser.IntegrationTitle, result?.IntegrationTitle);
        }

        [Fact]
        public async Task SystemUser_CreateAndDelegate_Returns_Error()
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

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create")
            {
                Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"))
            };

            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, createSystemUserResponse.StatusCode);  
            var problemDetails = await createSystemUserResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.Reportee_Orgno_NotFound.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task SystemUser_ListAll_Ok()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            Guid id = Guid.NewGuid();

            string systemId = "991825827_the_matrix";

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                IntegrationTitle = "IntegrationTitleValue",
                SystemId = systemId
            };

            HttpRequestMessage createSystemUserRequest = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}/create"); 
            createSystemUserRequest.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage createSystemUserResponse = await client.SendAsync(createSystemUserRequest, HttpCompletionOption.ResponseContentRead);

            SystemUser? shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await createSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, createSystemUserResponse.StatusCode);
            Assert.NotNull(shouldBeCreated);
            Assert.Equal("IntegrationTitleValue", shouldBeCreated.IntegrationTitle);

            int numberOfTestCases = 13;

            await CreateSeveralSystemUsers(client, numberOfTestCases, systemId);

            // Stream PAGE_SIZE (10)
            HttpClient streamClient = CreateClient();
            string[] prefixes = { "altinn", "digdir" };
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.admin", prefixes);
            streamClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string streamEndpoint = $"/authentication/api/v1/systemuser/internal/systemusers/stream";
            HttpRequestMessage streamMessage = new(HttpMethod.Get, streamEndpoint);
            HttpResponseMessage streamResponse = await streamClient.SendAsync(streamMessage, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
            var result = await streamResponse.Content.ReadFromJsonAsync<ItemStream<SystemUserRegisterDTO>>();
            Assert.NotNull(result);
            var list = result.Items.ToList();            
            Assert.Equal(list[0].IntegrationTitle, newSystemUser.IntegrationTitle);
            Assert.Equal(_paginationSize, list.Count);

            // Stream some more, should get only 2
            streamEndpoint = result.Links.Next!;
            HttpRequestMessage streamMessage2 = new(HttpMethod.Get, streamEndpoint);
            HttpResponseMessage streamResponse2 = await streamClient.SendAsync(streamMessage2, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, streamResponse2.StatusCode);
            var result2 = await streamResponse2.Content.ReadFromJsonAsync<ItemStream<SystemUserRegisterDTO>>();            
            Assert.NotNull(result2);
            var list2 = result2.Items.ToList();
            Assert.Equal(numberOfTestCases - _paginationSize +1, list2.Count);

            // Stream yet again, should get 0 new 
            streamEndpoint = result2.Links.Next!;
            HttpRequestMessage streamMessage3 = new(HttpMethod.Get, streamEndpoint);
            HttpResponseMessage streamResponse3 = await streamClient.SendAsync(streamMessage3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, streamResponse3.StatusCode);
            var result3 = await streamResponse3.Content.ReadFromJsonAsync<ItemStream<SystemUserRegisterDTO>>();
            Assert.NotNull(result3);
            var list3 = result3.Items.ToList();
            Assert.Empty(list3);
        }

        // Agent Tests
        [Fact]
        public async Task AgentSystemUser_Get_ListForPartyId_ReturnsListOK()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            listSystemUserRequst.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage listSystemUserResponse = await client2.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
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
        public async Task AgentSystemUser_Get_ListForPartyId_ReturnsForbidden()
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
        public async Task AgentSystemUser_Get_ListForPartyId_ReturnsUnathorized()
        {
            HttpClient client = CreateClient();

            int partyId = 500801;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);
        }

        /// <summary>
        /// Scenario where user does not have a valid token
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AgentSystemUser_Get_ListForPartyId_ReturnsEmptyList()
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));

            int partyId = 500000;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            var list = await response.Content.ReadFromJsonAsync<List<SystemUser>>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(list);
            Assert.Empty(list);
        }

        // Agent Tests
        [Fact]
        public async Task AgentSystemUser_Delegate_Post_OK()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackageAgent.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:forretningsforer-eiendom"

            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            listSystemUserRequst.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage listSystemUserResponse = await client2.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
            List<SystemUser>? list = JsonSerializer.Deserialize<List<SystemUser>>(await listSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 1);

            // Delegation of a Customer to the empty Agent System User
            string systemUserId = list[0].Id;
            string delegationEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}/delegation/";

            var delegationRequest = new AgentDelegationInputDto { CustomerId = Guid.NewGuid().ToString(), FacilitatorId = Guid.NewGuid().ToString() };

            HttpRequestMessage delegateMessage = new(HttpMethod.Post, delegationEndpoint);
            delegateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            delegateMessage.Content = JsonContent.Create(delegationRequest);
            HttpResponseMessage delegationResponse = await client2.SendAsync(delegateMessage, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.OK, delegationResponse.StatusCode);
        }

        // Agent Tests
        [Fact]
        public async Task AgentSystemUser_Delegate_Post_SystemUser_BadRequest()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"

            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            listSystemUserRequst.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage listSystemUserResponse = await client2.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
            List<SystemUser>? list = JsonSerializer.Deserialize<List<SystemUser>>(await listSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 0);

            // Delegation of a Customer to the empty Agent System User
            string systemUserId = Guid.NewGuid().ToString();
            string delegationEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}/delegation/";

            var delegationRequest = new AgentDelegationInputDto { CustomerId = Guid.NewGuid().ToString(), FacilitatorId = Guid.NewGuid().ToString() };

            HttpRequestMessage delegateMessage = new(HttpMethod.Post, delegationEndpoint);
            delegateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            delegateMessage.Content = JsonContent.Create(delegationRequest);
            HttpResponseMessage delegationResponse = await client2.SendAsync(delegateMessage, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, delegationResponse.StatusCode);            
        }

        // Agent Tests
        [Fact]
        public async Task AgentSystemUser_Delegate_Post_Unauthorized()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"

            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}");
            listSystemUserRequst.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage listSystemUserResponse = await client2.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
            List<SystemUser>? list = JsonSerializer.Deserialize<List<SystemUser>>(await listSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 1);

            // Delegation of a Customer to the empty Agent System User
            string systemUserId = list[0].Id;
            string delegationEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}/delegation/";

            var delegationRequest = new AgentDelegationInputDto { CustomerId = Guid.NewGuid().ToString(), FacilitatorId = Guid.NewGuid().ToString() };

            HttpRequestMessage delegateMessage = new(HttpMethod.Post, delegationEndpoint);

            // delegateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            delegateMessage.Content = JsonContent.Create(delegationRequest);
            HttpResponseMessage delegationResponse = await client2.SendAsync(delegateMessage, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, delegationResponse.StatusCode);
        }

        // Agent Tests
        [Fact]
        public async Task AgentSystemUser_Delegate_Post_BadRequest()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaerin" // Missing g

            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            Assert.Equal(HttpStatusCode.BadRequest, message.StatusCode);
        }

        [Fact]
        public async Task AgentSystemUser_Get_Delegations_ReturnsListOK()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid clientId = Guid.NewGuid();
            Guid facilitator = Guid.Parse("00000000-0000-0000-0005-000000000000");

            HttpRequestMessage listSystemUserRequst = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}/{facilitator}/{systemUserId}/delegations");
            listSystemUserRequst.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage listSystemUserResponse = await client2.SendAsync(listSystemUserRequst, HttpCompletionOption.ResponseContentRead);
            List<DelegationResponse>? list = JsonSerializer.Deserialize<List<DelegationResponse>>(await listSystemUserResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 1);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteCustomer_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = new Guid("0af0688f-4743-4697-acdd-8b2c13884f65");
            Guid delegationId = Guid.NewGuid();

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/delegation/{delegationId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteCustomer_ReturnsBadRequest()
        {
            int partyId = 500005;
            Guid facilitatorId = new Guid("02ba44dc-d80b-4493-a942-9b355d491da0");
            Guid delegationId = Guid.NewGuid();

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/delegation/{delegationId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.CustomerDelegation_FailedToRevoke.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteCustomer_ReturnsBadRequest_DelegationNotFound()
        {
            int partyId = 500005;
            Guid facilitatorId = new Guid("199912a2-86e1-4c8e-b010-c8c3956535a7");
            Guid delegationId = Guid.NewGuid();

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/delegation/{delegationId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_DelegationNotFound.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteCustomer_ReturnsBadRequest_PartyMismatch()
        {
            int partyId = 500005;
            Guid facilitatorId = new Guid("1765cf28-2554-4f3c-90c6-a269a01f46c8");
            Guid delegationId = Guid.NewGuid();

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/delegation/{delegationId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_DeleteDelegation_PartyMismatch.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteCustomer_ReturnsBadRequest_InvalidDelegationFacilitator()
        {
            int partyId = 500005;
            Guid facilitatorId = new Guid("cf814a90-1a14-4323-ae8b-72738abaab49");
            Guid delegationId = Guid.NewGuid();

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/delegation/{delegationId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_InvalidDelegationFacilitator.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteAgent_ReturnsOk()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = new Guid("aafe89c4-8315-4dfa-a16b-1b1592f2b651");

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteAgent_ReturnsBadRequest()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = new Guid("ca00ce4a-c30c-4cf7-9523-a65cd3a40232");

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
            var problemDetails = await response3.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_FailedToDeleteAgent.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteAgent_ReturnsBadRequest_HasDelegations()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = Guid.NewGuid();

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
            var problemDetails = await response3.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_HasDelegations.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteAgent_ReturnsOK_For_AssignmentNotFound()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = new Guid("32153b44-4da9-4793-8b8f-6aa4f7d17d17");

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        }

        [Fact]
        public async Task AgentSystemUser_DeleteAgent_ReturnsBadRequest_For_TooManyAssignment()
        {
            // Create System used for test
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            HttpClient client = CreateClient();
            string token = AddSystemUserRequestWriteTestTokenToClient(client);
            string endpoint = $"/authentication/api/v1/systemuser/request/vendor/agent";

            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            // Arrange
            CreateAgentRequestSystemUser req = new()
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

            AgentRequestSystemResponse? res = await message.Content.ReadFromJsonAsync<AgentRequestSystemResponse>();
            Assert.NotNull(res);
            Assert.Equal(req.ExternalRef, res.ExternalRef);

            //// Party Get Request
            HttpClient client2 = CreateClient();

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/agent/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            approveRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);

            string getEndpoint = $"/authentication/api/v1/systemuser/agent/{partyId}";

            HttpRequestMessage getAgent = new(HttpMethod.Get, getEndpoint);
            getAgent.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage getResponse = await client2.SendAsync(getAgent, HttpCompletionOption.ResponseHeadersRead);

            var systemUserApproveResponse = await getResponse.Content.ReadFromJsonAsync<List<SystemUser>>();
            Assert.NotNull(systemUserApproveResponse);

            Guid systemUserId = Guid.Parse(systemUserApproveResponse[0].Id);

            Guid facilitatorId = new Guid("23478729-1ffa-49c7-a3d0-6e0d08540e9a");

            HttpClient client3 = CreateClient();
            client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpRequestMessage request3 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/agent/{partyId}/{systemUserId}?facilitatorId={facilitatorId}");
            HttpResponseMessage response3 = await client3.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
            var problemDetails = await response3.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(Problem.AgentSystemUser_TooManyAssignments.Detail, problemDetails?.Detail);
        }

        [Fact]
        public async Task AgentSystemUser_GetClients_ReturnsListOK()
        {
            HttpClient client2 = CreateClient();

            // partyId of the system user that is used to fetch the clients
            int partyId = 500000;

            Guid clientId = Guid.NewGuid();
            Guid facilitator = Guid.NewGuid();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}/clients?facilitator={facilitator}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage clientListResponse = await client2.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            List<ConnectionDto>? list = JsonSerializer.Deserialize<List<ConnectionDto>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, clientListResponse.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count > 1);
        }

        [Fact]
        public async Task AgentSystemUser_GetClients_FilterByPackage_ReturnsListOK()
        {
            string accessPackage1 = "regnskapsforer-lonn";
            string accessPackage2 = "regnskapsforer-med-signeringsrettighet";

            HttpClient client2 = CreateClient();

            // partyId of the system user that is used to fetch the clients
            int partyId = 500000;

            Guid clientId = Guid.NewGuid();
            Guid facilitator = Guid.NewGuid();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}/clients?facilitator={facilitator}&packages={accessPackage1}&packages={accessPackage2}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage clientListResponse = await client2.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            List<ConnectionDto>? list = JsonSerializer.Deserialize<List<ConnectionDto>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, clientListResponse.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count == 4);
        }

        [Fact]
        public async Task AgentSystemUser_GetClients_Unauthorized()
        {
            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            HttpClient client2 = CreateClient();

            // partyId of the system user that is used to fetch the clients
            int partyId = 500000;

            Guid clientId = Guid.NewGuid();
            Guid facilitator = Guid.NewGuid();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}/clients?facilitator={facilitator}&packages={accessPackage}");
            
            HttpResponseMessage clientListResponse = await client2.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task AgentSystemUser_GetClients_Forbidden()
        {
            AccessPackage accessPackage = new()
            {
                Urn = "urn:altinn:accesspackage:skattnaering"
            };

            HttpClient client2 = CreateClient();

            // partyId of the system user that is used to fetch the clients
            int partyId = 500000;

            Guid clientId = Guid.NewGuid();
            Guid facilitator = new Guid("ca00ce4a-c30c-4cf7-9523-a65cd3a40232");

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/agent/{partyId}/clients?facilitator={facilitator}&packages={accessPackage}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3));
            HttpResponseMessage clientListResponse = await client2.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        private async Task CreateSeveralSystemUsers(HttpClient client, int paginationSize, string systemId)
        {
            var tasks = Enumerable.Range(0, paginationSize)
                              .Select(i => CreateSystemUser(client, i, systemId))
                              .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task CreateSystemUser(HttpClient client, int externalRef, string systemId)
        {
            string token = AddSystemUserRequestWriteTestTokenToClient(client);

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

            HttpClient client2 = CreateClient();
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

            int partyId = 500000;

            string approveEndpoint = $"/authentication/api/v1/systemuser/request/{partyId}/{res.Id}/approve";
            HttpRequestMessage approveRequestMessage = new(HttpMethod.Post, approveEndpoint);
            HttpResponseMessage approveResponseMessage = await client2.SendAsync(approveRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, approveResponseMessage.StatusCode);
        }

        private static string AddSystemUserRequestWriteTestTokenToClient(HttpClient client)
        {
            string[] prefixes = ["altinn", "digdir"];
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemuser.request.write", prefixes);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            return token;
        }

        private static AuthenticationHeaderValue AddAuthorizationTestTokenToRequest(string orgno)
        {
            string[] prefixes = ["altinn", "digdir"];
            string token = PrincipalUtil.GetOrgToken("digdir", orgno, "altinn:authentication/systemuser.request.write", prefixes);
            return new("Bearer", token);
        }

        private static string GetConfigPath()
        {
            string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SystemUserControllerTest).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder!, $"../../../appsettings.json");
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
