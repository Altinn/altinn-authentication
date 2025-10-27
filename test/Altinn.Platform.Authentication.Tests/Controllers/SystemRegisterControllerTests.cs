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
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Integration.ResourceRegister;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
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
    public class SystemRegisterControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
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
            services.AddSingleton<ISystemUserService, SystemUserService>();
            services.AddSingleton<ISystemRegisterService, SystemRegisterService>();
            services.AddSingleton<IResourceRegistryClient, ResourceRegistryClientMock>();
            services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
            SetupDateTimeMock();
            SetupGuidMock();
        }

        [Fact]
        public async Task SystemRegister_Create_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            HttpClient getClient = GetAuthenticatedClient(Write, ValidOrg);
            await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getClient, "991825827_the_matrix", "ChangeLogCreate");
        }

        [Fact]
        public async Task SystemRegister_Create_WithApp_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithApp.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WithResourceAndApp_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithResourceAndApp.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WithAccessPackage_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidRequest.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_BadRequest_SystemId()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidSystemId.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemRegister_Invalid_SystemId_Spaces.ErrorCode);
            Assert.Equal("/registersystemrequest/systemid", error.Paths.First(p => p.Equals("/registersystemrequest/systemid")));
            Assert.Equal("System ID cannot have spaces in id (leading, trailing or in between the id)", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_DuplicateSystem_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            HttpClient client2 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage existingSystemResponse = await SystemRegisterTestHelper.CreateSystemRegister(client2, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, existingSystemResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await existingSystemResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemRegister_SystemId_Exists.ErrorCode);
            Assert.Equal("/registersystemrequest/systemid", error.Paths.First(p => p.Equals("/registersystemrequest/systemid")));
            Assert.Equal("The system id already exists", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidSystemIdFormat_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidSystemIdFormat.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_Invalid_SystemId_Format.ErrorCode);
            Assert.Equal("/registersystemrequest/systemid", error.Paths.Single(p => p.Equals("/registersystemrequest/systemid")));
            Assert.Equal("The system id does not match the format orgnumber_xxxx...", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidResourceIdFormat_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidResourceIdFormat.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_InvalidFormat.ErrorCode);
            Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
            Assert.Equal("One or more resource id is in wrong format. The valid format is urn:altinn:resource", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidResourceId_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidResource.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_DoesNotExist.ErrorCode);
            Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
            Assert.Equal("One or all the resources in rights is not found in altinn's resource register", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidAccessPackages_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidAccessPackage.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_NotValid.ErrorCode);
            Assert.Equal("/registersystemrequest/accesspackages", error.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
            Assert.Equal("One or all the accesspackage(s) is not found in altinn's access packages or is not delegable", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_DuplicateResource_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterDuplicateResource.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_Duplicates.ErrorCode);
            Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
            Assert.Equal("One or more duplicate rights found", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_DuplicateAccessPackage_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterDuplicateAccessPackage.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_Duplicates.ErrorCode);
            Assert.Equal("/registersystemrequest/accesspackages", error.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
            Assert.Equal("One or more duplicate access package(s) found", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidRedirectUrl_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidRedirectUrl.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_InValid_RedirectUrlFormat.ErrorCode);
            Assert.Equal("/registersystemrequest/allowedredirecturls", error.Paths.Single(p => p.Equals("/registersystemrequest/allowedredirecturls")));
            Assert.Equal("One or more of the redirect urls format is not valid. The valid format is https://xxx.xx", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_InvalidOrgIdentifier_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidOrgIdentifier.json";
            HttpClient client = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(client, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemRegister_InValid_Org_Identifier.ErrorCode);
            Assert.Equal("/registersystemrequest/vendor/id", error.Paths.First(p => p.Equals("/registersystemrequest/vendor/id")));
            Assert.Equal("the org number identifier is not valid ISO6523 identifier", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Update_Rights_Success()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRight.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/rights");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.OK, updateResponse.StatusCode);

                HttpClient getClient = GetAuthenticatedClient(Write, ValidOrg);
                await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getClient, systemID, "ChangeLogRightsUpdate");
            }
        }

        [Fact]
        public async Task SystemRegister_Update_Rights_DeletedSystem_ReturnsBadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                string systemID = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{systemID}/");
                HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRight.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
               
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/rights");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
                string body = await updateResponse.Content.ReadAsStringAsync();
                Assert.Equal("Cannot update a system marked as deleted.", body);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Success_Admin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                string systemID = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{systemID}/");
                HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackages.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/accesspackages");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
                string body = await updateResponse.Content.ReadAsStringAsync();
                Assert.Equal("Cannot update a system marked as deleted.", body);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_DeletedSystem_ReturnsBadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackages.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/accesspackages");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.OK, updateResponse.StatusCode);

                HttpClient getClient = GetAuthenticatedClient(Admin, ValidOrg);
                await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getClient, "991825827_the_matrix", "ChangeLogAPUpdate");
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Success_Owner_NotAdmin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.write", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackages.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/accesspackages");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.OK, updateResponse.StatusCode);

                HttpClient getClient = GetAuthenticatedClient(Write, ValidOrg);
                await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getClient, "991825827_the_matrix", "ChangeLogAPUpdate");
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Forbid_NotOwner_NotAdmin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "974761076", "altinn:authentication/systemregister.write", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackages.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/accesspackages");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.Forbidden, updateResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRightInvalidRequest.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/rights");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_DuplicateResource_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRightDuplicate.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/rights");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
                AltinnValidationProblemDetails problemDetails = await updateResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
                Assert.NotNull(problemDetails);
                AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_Duplicates.ErrorCode);
                Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
                Assert.Equal("One or more duplicate rights found", error.Detail);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_DuplicateAccessPackage_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateAccessPackageDuplicate.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/accesspackages");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
                AltinnValidationProblemDetails problemDetails = await updateResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
                Assert.NotNull(problemDetails);
                AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_Duplicates.ErrorCode);
                Assert.Equal("/registersystemrequest/accesspackages", error.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
                Assert.Equal("One or more duplicate access package(s) found", error.Detail);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_ResourceIdDoesNotExist_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.IsSuccessStatusCode)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Arrange
                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/UpdateRightResourceIdNotExist.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string systemID = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemID}/rights");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
                AltinnValidationProblemDetails problemDetails = await updateResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
                Assert.NotNull(problemDetails);
                Assert.Single(problemDetails.Errors);
                AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_DoesNotExist.ErrorCode);
                Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
                Assert.Equal("One or all the resources in rights is not found in altinn's resource register", error.Detail);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ListofAll()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            string dataFileName01 = "Data/SystemRegister/Json/SystemRegister01.json";
            HttpClient createClient2 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response01 = await SystemRegisterTestHelper.CreateSystemRegister(createClient2, dataFileName01);

            string dataFileName02 = "Data/SystemRegister/Json/SystemRegisterWithAccessPackageNull.json";
            HttpClient createClient3 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response02 = await SystemRegisterTestHelper.CreateSystemRegister(createClient3, dataFileName02);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response01.StatusCode == System.Net.HttpStatusCode.OK && response02.StatusCode == System.Net.HttpStatusCode.OK)
            {
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister");
                HttpResponseMessage getAllResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<RegisteredSystemDTO> list = JsonSerializer.Deserialize<List<RegisteredSystemDTO>>(await getAllResponse.Content.ReadAsStringAsync(), _options);
                Assert.Equal(3, list.Count);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ListofAll_NoData()
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister");
            HttpResponseMessage getAllResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<RegisteredSystemResponse> list = JsonSerializer.Deserialize<List<RegisteredSystemResponse>>(await getAllResponse.Content.ReadAsStringAsync(), _options);
            Assert.True(list.Count == 0);
        }

        [Fact]
        public async Task SystemRegister_Get_ListofAll_For_Vendor()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            string dataFileName01 = "Data/SystemRegister/Json/SystemRegister01.json";
            HttpClient createClient2 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response01 = await SystemRegisterTestHelper.CreateSystemRegister(createClient2, dataFileName01);
            Assert.Equal(System.Net.HttpStatusCode.OK, response01.StatusCode);

            string dataFileName03 = "Data/SystemRegister/Json/SystemRegister_altinn.json";
            HttpClient createClient3 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response03 = await SystemRegisterTestHelper.CreateSystemRegister(createClient3, dataFileName03);
            Assert.Equal(System.Net.HttpStatusCode.OK, response03.StatusCode);

            string dataFileName04 = "Data/SystemRegister/Json/SystemRegisterWithAccessPackageNull.json";
            HttpClient createClient4 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response04 = await SystemRegisterTestHelper.CreateSystemRegister(createClient4, dataFileName04);
            Assert.Equal(System.Net.HttpStatusCode.OK, response04.StatusCode);

            HttpClient client = GetAuthenticatedClient(Write, "312529750");
                
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor");
            HttpResponseMessage getAllResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<RegisteredSystemDTO> list = JsonSerializer.Deserialize<List<RegisteredSystemDTO>>(await getAllResponse.Content.ReadAsStringAsync(), _options);
            Assert.Single(list);            
        }

        [Fact]
        public async Task SystemRegister_Get_Systems_Vendor_NoData()
        {
            HttpClient client = GetAuthenticatedClient(Write, "312529750");

            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor");
            HttpResponseMessage getAllResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<RegisteredSystemResponse> list = JsonSerializer.Deserialize<List<RegisteredSystemResponse>>(await getAllResponse.Content.ReadAsStringAsync(), _options);
            Assert.True(list.Count == 0);
        }

        [Fact]
        public async Task SystemRegister_Get()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string systemRegister = File.OpenText("Data/SystemRegister/Json/SystemRegisterResponse.json").ReadToEnd();
                RegisteredSystemResponse expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(systemRegister, options);

                string systemId = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
                HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                RegisteredSystemResponse actualRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getResponse.Content.ReadAsStringAsync(), _options);
                AssertionUtil.AssertRegisteredSystem(expectedRegisteredSystem, actualRegisteredSystem);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_Forbid_IfNotOwner()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string orgToken = PrincipalUtil.GetOrgToken("skatt", "974761076", "altinn:authentication/systemregister.write", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgToken);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string systemRegister = File.OpenText("Data/SystemRegister/Json/SystemRegisterResponse.json").ReadToEnd();
                RegisteredSystemResponse expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(systemRegister, options);

                string systemId = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
                HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegisterDTO_Get()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string systemRegisterDTO = File.OpenText("Data/SystemRegister/Json/SystemRegisterDtoResponse.json").ReadToEnd();
                RegisteredSystemDTO expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemDTO>(systemRegisterDTO, options);

                string systemId = "991825827_the_matrix";
                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{systemId}");
                HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                RegisteredSystemDTO actualRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemDTO>(await getResponse.Content.ReadAsStringAsync(), _options);
                AssertionUtil.AssertRegisteredSystemDTO(expectedRegisteredSystem, actualRegisteredSystem);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_NotFound()
        {
            HttpClient client = CreateClient();
            string[] prefixes = { "altinn", "digdir" };
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string systemRegister = File.OpenText("Data/SystemRegister/Json/SystemRegister.json").ReadToEnd();
            RegisteredSystemResponse expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(systemRegister, options);

            string systemId = "991825827_the_matrix";
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
            HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.NoContent, getResponse.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<Right> list = JsonSerializer.Deserialize<List<Right>>(await rightsResponse.Content.ReadAsStringAsync(), _options);
                Assert.Equal("ske-krav-og-betalinger", list[0].Resource[0].Value);
                Assert.True(list.Count == 1);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights_App()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithApp.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            var expectedRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "app_ttd_endring-av-navn-v2" }
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<Right> list = JsonSerializer.Deserialize<List<Right>>(await rightsResponse.Content.ReadAsStringAsync(), _options);

                // Assert
                Assert.True(list.Count == 1);
                Assert.Equal(expectedRights.Count, list[0].Resource.Count);
                for (int i = 0; i < expectedRights.Count; i++)
                {
                    Assert.Equal(expectedRights[i].Id, list[0].Resource[i].Id);
                    Assert.Equal(expectedRights[i].Value, list[0].Resource[i].Value);
                }
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights_App_OldFormat()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithApp.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            var expectedRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:org", Value = "ttd" },
                new AttributePair { Id = "urn:altinn:app", Value = "endring-av-navn-v2" },
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights?useoldformatforapp=true");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<Right> list = JsonSerializer.Deserialize<List<Right>>(await rightsResponse.Content.ReadAsStringAsync(), _options);

                // Assert
                Assert.True(list.Count == 1);
                Assert.Equal(expectedRights.Count, list[0].Resource.Count);
                for (int i = 0; i < expectedRights.Count; i++)
                {
                    Assert.Equal(expectedRights[i].Id, list[0].Resource[i].Id);
                    Assert.Equal(expectedRights[i].Value, list[0].Resource[i].Value);
                }
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights_App_Resource()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithResourceAndApp.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            var expectedAppRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "app_ttd_endring-av-navn-v2" }
            };

            var expectedResourceRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "ske-krav-og-betalinger" },
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app_and_resource";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<Right> list = JsonSerializer.Deserialize<List<Right>>(await rightsResponse.Content.ReadAsStringAsync(), _options);

                // Assert
                Assert.True(list.Count == 2);
                Assert.Equal(expectedAppRights.Count, list[0].Resource.Count);
                Assert.Equal(expectedResourceRights.Count, list[1].Resource.Count);
                for (int i = 0; i < expectedAppRights.Count; i++)
                {
                    Assert.Equal(expectedAppRights[i].Id, list[0].Resource[i].Id);
                    Assert.Equal(expectedAppRights[i].Value, list[0].Resource[i].Value);
                }

                for (int i = 0; i < expectedResourceRights.Count; i++)
                {
                    Assert.Equal(expectedResourceRights[i].Id, list[1].Resource[i].Id);
                    Assert.Equal(expectedResourceRights[i].Value, list[1].Resource[i].Value);
                }
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights_App_Resource_OldAppFormat()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithResourceAndApp.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            var expectedAppRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:org", Value = "ttd" },
                new AttributePair { Id = "urn:altinn:app", Value = "endring-av-navn-v2" },
            };

            var expectedResourceRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "ske-krav-og-betalinger" },
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app_and_resource";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights?useoldformatforapp=true");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<Right> list = JsonSerializer.Deserialize<List<Right>>(await rightsResponse.Content.ReadAsStringAsync(), _options);

                // Assert
                Assert.True(list.Count == 2);
                Assert.Equal(expectedAppRights.Count, list[0].Resource.Count);
                Assert.Equal(expectedResourceRights.Count, list[1].Resource.Count);
                for (int i = 0; i < expectedAppRights.Count; i++)
                {
                    Assert.Equal(expectedAppRights[i].Id, list[0].Resource[i].Id);
                    Assert.Equal(expectedAppRights[i].Value, list[0].Resource[i].Value);
                }

                for (int i = 0; i < expectedResourceRights.Count; i++)
                {
                    Assert.Equal(expectedResourceRights[i].Id, list[1].Resource[i].Id);
                    Assert.Equal(expectedResourceRights[i].Value, list[1].Resource[i].Value);
                }
            }
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights_NoRights()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/rights");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, rightsResponse.StatusCode);
                Assert.Equal("[]", await rightsResponse.Content.ReadAsStringAsync());
            }
        }

        [Fact]
        public async Task SystemRegister_Get_AccessPackages()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/accesspackages");
                HttpResponseMessage responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<AccessPackage> list = JsonSerializer.Deserialize<List<AccessPackage>>(await responseMessage.Content.ReadAsStringAsync(), _options);
                Assert.Equal("urn:altinn:accesspackage:skatt-naering", list[0].Urn);
                Assert.True(list.Count == 3);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_AccessPackages_NoAccessPackages()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);            

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, null, 3, true));

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/accesspackages");
                HttpResponseMessage responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
                Assert.Equal("[]", await responseMessage.Content.ReadAsStringAsync());
            }
        }

        [Fact]
        public async Task SystemRegister_Delete_System()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);          

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, rightsResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Delete_System_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrixx";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.BadRequest, rightsResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Delete_System_BySystemOwner()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.write", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, rightsResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Delete_System_BySystemOwner_WrongScope()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.read", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.Forbidden, rightsResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Delete_System_BySystemOwner_MismatchInOrg()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825826", "altinn:authentication/systemregister.write", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage rightsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.Forbidden, rightsResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_System()
        {
            // Post original System
            const string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Prepare updated system request
            string systemId = "991825827_the_matrix";
            HttpClient client = CreateClient();
            string[] prefixes = ["altinn", "digdir"];
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateRequest.json");
            StreamContent content = new StreamContent(dataStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Run update request with two new client_id's - removing one existing and adding two new ones
            HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/");
            request.Content = content;
            HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Get updated system to verify fields were updated
            HttpResponseMessage getSystemResponse = await GetSystemRegister(systemId);
            Assert.Equal(HttpStatusCode.OK, getSystemResponse.StatusCode);

            RegisteredSystemResponse actualUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getSystemResponse.Content.ReadAsStringAsync(), _options);
            string systemRegister = await File.OpenText("Data/SystemRegister/Json/SystemRegisterUpdateResponse.json").ReadToEndAsync();
            RegisteredSystemResponse expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(systemRegister, options);

            // Assert updates were made
            AssertionUtil.AssertRegisteredSystem(expectedRegisteredSystem, actualUpdatedSystem);
           
            HttpClient getClient = GetAuthenticatedClient(Write, ValidOrg);
            await SystemRegisterTestHelper.GetAndAssertSystemChangeLog(getClient, "991825827_the_matrix", "ChangeLogUpdate");

            // Verify you can create new system with old (deleted) clientIds
            string filename = "Data/SystemRegister/Json/SystemRegisterClientIdsExist.json";
            HttpClient createClient2 = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage responseNewSystem = await SystemRegisterTestHelper.CreateSystemRegister(createClient2, filename);
            var resp = await responseNewSystem.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, responseNewSystem.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Update_System_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string systemId = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateBadRequest.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
                AltinnValidationProblemDetails problemDetails = await updateResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
                Assert.NotNull(problemDetails);
                Assert.Equal(2, problemDetails.Errors.Count);
                AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_ResourceId_DoesNotExist.ErrorCode);
                Assert.Equal("/registersystemrequest/rights/resource", error.Paths.Single(p => p.Equals("/registersystemrequest/rights/resource")));
                Assert.Equal("One or all the resources in rights is not found in altinn's resource register", error.Detail);

                AltinnValidationError error01 = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_Duplicates.ErrorCode);
                Assert.Equal("/registersystemrequest/accesspackages", error01.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
                Assert.Equal("One or more duplicate access package(s) found", error01.Detail);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_System_InvalidRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string systemId = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateInvalid.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Create_WrongScope_Forbidden()
        {
            HttpClient client = CreateClient();
            string token = PrincipalUtil.GetOrgToken("skd", "974761076", "altinn:authentication/systemregister");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
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
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WriteScope_OrgMismatch_Forbidden()
        {
            HttpClient client = CreateClient();
            string token = PrincipalUtil.GetOrgToken("skd", "974761076", "altinn:authentication/systemregister.write");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
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
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WriteScope_SystemOwner_Success()
        {
            HttpClient client = CreateClient();
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.write");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
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
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_NotDelegableAccessPackage_BadRequest()
        {
            HttpClient client = CreateClient();
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.write");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithNotDelegableAccessPackage.json";
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
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_NotValid.ErrorCode);
            Assert.Equal("/registersystemrequest/accesspackages", error.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
            Assert.Equal("One or all the accesspackage(s) is not found in altinn's access packages or is not delegable", error.Detail);
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
        public async Task SystemRegister_Rollback_Test()
        {
            const string systemId = "991825827_the_matrix";
            List<string> clientIdsInFirstSystem = [Guid.NewGuid().ToString()];

            // New update
            List<string> validClientIds = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];

            // IsVisible = false
            RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem, isVisible: false);
            HttpResponseMessage response = await CreateSystemRegister(originalSystem);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Invalid redirectUrl and trying to set "InVisible: true"
            RegisterSystemRequest updateIsVisibleTrue = CreateSystemRegisterRequest(systemId, validClientIds, true, "htts://vg.no");
            var resp = await PutSystemRegisterAsync(updateIsVisibleTrue, systemId);
            var stringResp = await resp.Content.ReadAsStringAsync();

            // make sure we noticed it failed on postgres update
            Assert.Contains("Npgsql.PostgresException", stringResp);

            HttpResponseMessage getSystemResponse = await GetSystemRegister(systemId);
            Assert.Equal(HttpStatusCode.OK, getSystemResponse.StatusCode);

            // verify the second one failed and did not update isVisible = true
            RegisteredSystemResponse noneUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getSystemResponse.Content.ReadAsStringAsync(), _options);
            Assert.False(noneUpdatedSystem.IsVisible);
            Assert.Equal(noneUpdatedSystem.ClientId, clientIdsInFirstSystem);
        }

        [Fact]
        public async Task SystemRegister_Delete_Several_Clients_Test()
        {
            const string systemId = "991825827_the_matrix";
            List<string> clientIdsInFirstSystem = ["f5d441dc-e4fe-482b-b8ff-164951689c9d", "0d767db9-1713-47cb-bac5-ce3439f450c2"];

            RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
            HttpResponseMessage response = await CreateSystemRegister(originalSystem);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Run update with no clientIds
            List<string> newClientIds = [];

            // Keep using the same clientId, but update something else
            RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds, true);
            var resp = await PutSystemRegisterAsync(updatedSystem, systemId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            HttpResponseMessage getSystemResponse = await GetSystemRegister(systemId);
            Assert.Equal(HttpStatusCode.OK, getSystemResponse.StatusCode);

            // Assert all client ids were removed
            RegisteredSystemResponse actualUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getSystemResponse.Content.ReadAsStringAsync(), _options);
            Assert.Empty(actualUpdatedSystem.ClientId);
        }

        [Fact]
        public async Task SystemRegister_DuplicateClientIds_Test()
        {
            // Prepare
            const string systemId = "991825827_the_matrix";
            List<string> clientIdsInFirstSystem = ["09f040c6-dfc8-4a96-bd74-11bb2708ef81", "bc2df960-1d22-40be-a39f-b3b698bb2868"];
            RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
            HttpResponseMessage response = await CreateSystemRegister(originalSystem);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Duplicate clientIds
            List<string> duplicateClientIds = ["09f040c6-dfc8-4a96-bd74-11bb2708ef81", "09f040c6-dfc8-4a96-bd74-11bb2708ef81"];
            RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, duplicateClientIds);
            var resp = await PutSystemRegisterAsync(updatedSystem, systemId);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

            string content = await resp.Content.ReadAsStringAsync();
            Assert.Contains(ValidationErrors.SystemRegister_Duplicate_ClientIds.Detail, content);
        }

        [Fact]
        public async Task SystemRegister_EmptyClientId_Test()
        {
            // Prepare
            const string systemId = "991825827_the_matrix";

            // Empty to see if we can update
            List<string> clientIdsInFirstSystem = [];
            RegisterSystemRequest originalSystem = CreateSystemRegisterRequest(systemId, clientIdsInFirstSystem);
            HttpResponseMessage response = await CreateSystemRegister(originalSystem);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // New clientIds
            List<string> newClientIds = ["dcbebc87-1e84-4ace-928a-220d4e557bb4", "070bebf2-c1b6-4efb-9e6b-be1927c473d8"];
            RegisterSystemRequest proposedUpdate = CreateSystemRegisterRequest(systemId, newClientIds);
            var resp = await PutSystemRegisterAsync(proposedUpdate, systemId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            HttpResponseMessage responseGet = await GetSystemRegister(systemId);
            Assert.Equal(HttpStatusCode.OK, responseGet.StatusCode);

            RegisteredSystemResponse actualUpdatedSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await responseGet.Content.ReadAsStringAsync(), _options);
            Assert.True(actualUpdatedSystem.ClientId.Count == proposedUpdate.ClientId.Count);
            Assert.Equal(proposedUpdate.ClientId, actualUpdatedSystem.ClientId);
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
            Assert.Contains("The system ID in the request body does not match the system ID in the URL", content);
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

            List<string> clientIdsInFirstSystem = ["fda56404-f022-46e8-929c-f38b6de2c195", "a0ac009e-0d0b-44bd-b713-95f88355b14b", "9e8d58f8-9b3a-4d5b-95f6-78994ffd0152"];
            HttpResponseMessage responseFirst = await CreateAndAssertSystemAsync(systemId, clientIdsInFirstSystem);
            Assert.Equal(HttpStatusCode.OK, responseFirst.StatusCode);

            List<string> clientIdsSecondSystem = ["f204bf11-c92b-4ba1-a3ca-278dd290efdc"];
            await CreateAndAssertSystemAsync(systemIdSecondSystem, clientIdsSecondSystem);

            // Running update with one new clientId and also one old from a second system
            List<string> newClientIds = ["1cd178af-806e-40db-b050-46e60c1d112c", clientIdsSecondSystem[0]];
            RegisterSystemRequest updatedSystem = CreateSystemRegisterRequest(systemId, newClientIds);

            // Expecting bad request here
            HttpResponseMessage resp = await PutSystemRegisterAsync(updatedSystem, systemId);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            string content = await resp.Content.ReadAsStringAsync();
            Assert.Contains(ValidationErrors.SystemRegister_ClientID_Exists.Detail, content);
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
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}/changelog");
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

        [Fact]
        public async Task GetSystemChangeLog_InvalidSystemId_ReturnsNotFound()
        {
            // Post original System
            const string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Write, ValidOrg);

            string systemId = "991825827_the_matrix";

            // Get change log
            HttpClient getChangeLogClient = GetAuthenticatedClient(Write, ValidOrg);
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}/changelog");
            HttpResponseMessage getResponse = await getChangeLogClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Update_DeletedSystem_ReturnsBadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpClient createClient = GetAuthenticatedClient(Admin, ValidOrg);
            HttpResponseMessage response = await SystemRegisterTestHelper.CreateSystemRegister(createClient, dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                string[] prefixes = { "altinn", "digdir" };
                string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpRequestMessage request = new(HttpMethod.Delete, $"/authentication/api/v1/systemregister/vendor/{name}/");
                HttpResponseMessage deleteResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateRequest.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpRequestMessage updateRequest = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{name}/");
                updateRequest.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(updateRequest, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
                string body = await updateResponse.Content.ReadAsStringAsync();
                Assert.Equal("Cannot update a system marked as deleted.", body);
            }
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

        private static RegisterSystemRequest CreateSystemRegisterRequest(string systemId, List<string> clientIds, bool isVisible = false, string redirecturl = "https://altinn.no")
        {
            return new RegisterSystemRequest
            {
                Id = systemId,
                Vendor = new VendorInfo
                {
                    Authority = "iso6523-actorid-upis",
                    ID = $"0192:991825827"
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
                Rights =
                [
                    new Right
                    {
                        Resource =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "ske-krav-og-betalinger"
                            }
                        ]
                    }
                ],
                ClientId = clientIds,
                AllowedRedirectUrls =
                [
                    new Uri("https://vg.no"),
                    new Uri("https://nrk.no"),
                    new Uri(redirecturl)
                ],
                IsVisible = isVisible
            };
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

        private async Task<HttpResponseMessage> GetSystemRegister(string systemId)
        {
            HttpClient client = CreateClient();
            string[] prefixes = { "altinn", "digdir" };
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", "altinn:authentication/systemregister.admin", prefixes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
            HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            return getResponse;
        }

        public async Task<List<string>> RunParallelSystemRegisterUpdates(List<RegisterSystemRequest> updates)
        {
            List<string> responseBodies = [];
            List<Task<HttpResponseMessage>> tasks = updates
                .Select(updateDto => PutSystemRegisterAsync(updateDto, updateDto.Id))
                .ToList();

            HttpResponseMessage[] responses = await Task.WhenAll(tasks);

            foreach (HttpResponseMessage response in responses)
            {
                Console.WriteLine($"Status: {response.StatusCode}");
                string body = await response.Content.ReadAsStringAsync();
                responseBodies.Add(body);
            }

            return responseBodies;
        }
    }
}