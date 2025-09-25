using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.AccessManagement.Tests.Mocks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
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
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    public class SystemUserClientDelegationControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

        private readonly Mock<ISystemUserRepository> _systemUserRepository = new();
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

        private readonly Mock<TimeProvider> timeProviderMock = new();
        private readonly Mock<IGuidService> guidService = new();
        private readonly Mock<IEventsQueueClient> _eventQueue = new();
        private readonly Mock<ISystemUserRepository> _systemUserRepo = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            string configPath = GetConfigPath();

            WebHostBuilder builder = new();

            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddJsonFile(configPath);
            });

            var configuration = new ConfigurationBuilder()
              .AddJsonFile(configPath)
              .Build();
            configuration.GetSection("GeneralSettings:EnableOidc").Value = "false";
            configuration.GetSection("GeneralSettings:ForceOidc").Value = "false";
            configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = "altinn";

            IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

            services.Configure<GeneralSettings>(generalSettingSection);
            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
            services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();

            services.AddSingleton(timeProviderMock.Object);
            services.AddSingleton(guidService.Object);
            services.AddSingleton(_systemUserRepository.Object);
            services.AddSingleton<IUserProfileService>(_userProfileService.Object);
            services.AddSingleton<ISblCookieDecryptionService>(_sblCookieDecryptionService.Object);
            services.AddSingleton<IPDP, PDPPermitMock>();
            services.AddSingleton<IPartiesClient, PartiesClientMock>();
            services.AddSingleton<ISystemUserService, SystemUserService>();
            services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
            SetupGuidMock();
            SetupSystemUserRepositoryMock();
        }

        private static string GetConfigPath()
        {
            string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder!, $"../../../appsettings.json");
        }

        private void SetupGuidMock()
        {
            guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
        }

        private void SetupSystemUserRepositoryMock()
        {
            Guid validSystemUserId = new Guid("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            _systemUserRepository
            .Setup(r => r.GetSystemUserById(It.Is<Guid>(id => id == validSystemUserId)))
            .ReturnsAsync(new SystemUser
            {
                Id = "b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4",
                ReporteeOrgNo = "123456789",
                AccessPackages = new List<AccessPackage> { new AccessPackage { Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet" } }
            });
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientInfoPaginated<ClientInfo> result = JsonSerializer.Deserialize<ClientInfoPaginated<ClientInfo>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Items.Count() > 0);
            Assert.True(result.Links.Next is null);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ValidRequest_ReturnsForbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ValidRequest_ReturnsUnAuthorized()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientInfoPaginated<ClientInfo> result = JsonSerializer.Deserialize<ClientInfoPaginated<ClientInfo>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Items.Count() > 0);
            Assert.True(result.Links.Next is null);
        }

        [Fact]
        public async Task GetClientsDelegatedForASystemUser_ValidRequest_ReturnsForbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task GetClientsDelegatedForASystemUser_ValidRequest_ReturnsUnAuthorized()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task AddClientToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientDelegationResponse clientDelegationResponse = JsonSerializer.Deserialize<ClientDelegationResponse>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(clientDelegationResponse is not null);
            Assert.Equal(clientId, clientDelegationResponse.Client);
            Assert.Equal(systemUserId, clientDelegationResponse.Agent);
        }

        [Fact]
        public async Task AddClientToSystemUser_InValidSystemUserId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("d54a721a-b231-4e28-9245-782697ed2bbb");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new("fd9d93c7-1dd7-45bc-9772-6ba977b3cd36");
            Guid systemUserId = new("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientDelegationResponse clientDelegationResponse = JsonSerializer.Deserialize<ClientDelegationResponse>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(clientDelegationResponse is not null);
            Assert.Equal(clientId, clientDelegationResponse.Client);
            Assert.Equal(systemUserId, clientDelegationResponse.Agent);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_InValidSystemUserId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("d54a721a-b231-4e28-9245-782697ed2bbb");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
        }
    }
}
