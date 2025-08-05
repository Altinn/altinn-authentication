using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Helpers;
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
    public class SystemChangeLogControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

        private readonly Mock<TimeProvider> timeProviderMock = new Mock<TimeProvider>();
        private readonly Mock<IGuidService> guidService = new Mock<IGuidService>();
        private readonly Mock<IEventsQueueClient> _eventQueue = new Mock<IEventsQueueClient>();

        private readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public const string Admin = "altinn:authentication/systemregister.admin";
        public const string Write = "altinn:authentication/systemregister.write";
        
        public const string ValidOrg = "991825827";
        public const string InvalidOrg = "965714643";

        protected HttpClient GetAuthenticatedClient(string scope, string org)
        {
            var client = CreateClient();
            string[] prefixes = { "altinn", "digdir" };
            string token = PrincipalUtil.GetOrgToken("digdir", org, scope, prefixes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

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
            services.AddSingleton<ISystemChangeLogService, SystemChangeLogService>();
            services.AddSingleton<ISystemUserService, SystemUserService>();
            services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
            services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
            services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
            SetupDateTimeMock();
            SetupGuidMock();
        }

        [Fact]
        public async Task GetSystemChangeLog_ValidOrg_ReturnsOk()
        {
            // Post original System
            const string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Write, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Prepare updated system request
            string systemId = "991825827_the_matrix";
            HttpClient updateClient = GetAuthenticatedClient(Write, ValidOrg);

            Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateRequest.json");
            StreamContent content = new StreamContent(dataStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Run update request with two new client_id's - removing one existing and adding two new ones
            HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/");
            request.Content = content;
            HttpResponseMessage updateResponse = await updateClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Update Rigts only
            Stream rightsDataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRight.json");
            HttpClient rightsClient = GetAuthenticatedClient(Write, ValidOrg);
            StreamContent rightsContent = new StreamContent(rightsDataStream);
            rightsContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpRequestMessage rightsRequest = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/rights");
            rightsRequest.Content = rightsContent;
            HttpResponseMessage rightsUpdateResponse = await rightsClient.SendAsync(rightsRequest, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(System.Net.HttpStatusCode.OK, rightsUpdateResponse.StatusCode);

            // Update Access Package only
            Stream accessPackageDataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackages.json");
            HttpClient accessPackageClient = GetAuthenticatedClient(Write, ValidOrg);
            StreamContent accessPackageContent = new StreamContent(accessPackageDataStream);
            accessPackageContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpRequestMessage acccessPackageRequest = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/accesspackages");
            acccessPackageRequest.Content = accessPackageContent;
            HttpResponseMessage accessPackageResponse = await accessPackageClient.SendAsync(acccessPackageRequest, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(System.Net.HttpStatusCode.OK, accessPackageResponse.StatusCode);

            // Get change log
            HttpClient getChangeLogClient = GetAuthenticatedClient(Write, ValidOrg);
            await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getChangeLogClient, systemId, "ChangeLogAll");
        }

        [Fact]
        public async Task GetSystemChangeLog_InvalidOrg_ReturnsForbidden()
        {
            // Post original System
            const string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Write, ValidOrg);

            string systemId = "991825827_the_matrix";
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            HttpClient getChangeLogClient = GetAuthenticatedClient(Write, InvalidOrg);
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemchangelog/{systemId}");
            HttpResponseMessage getResponse = await getChangeLogClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        }

        [Fact]
        public async Task GetSystemChangeLog_Admin_ReturnsOk()
        {
            // Post original System
            const string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Write, ValidOrg);

            string systemId = "991825827_the_matrix";
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // Get change log
            HttpClient getChangeLogClient = GetAuthenticatedClient(Admin, ValidOrg);
            await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getChangeLogClient, systemId, "ChangeLogCreate");
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
    }
}
