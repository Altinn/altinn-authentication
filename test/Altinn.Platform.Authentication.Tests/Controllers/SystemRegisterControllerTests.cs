using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Policy;
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
using Azure;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault.Models;
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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WithApp_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithApp.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WithResourceAndApp_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithResourceAndApp.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_WithAccessPackage_Success()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithAccessPackage.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterInvalidRequest.json";

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SystemRegister_Create_DuplicateSystem_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            HttpResponseMessage existingSystemResponse = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            AltinnValidationProblemDetails problemDetails = await response.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Single(problemDetails.Errors);
            AltinnValidationError error = problemDetails.Errors.Single(e => e.ErrorCode == ValidationErrors.SystemRegister_AccessPackage_NotValid.ErrorCode);
            Assert.Equal("/registersystemrequest/accesspackages", error.Paths.Single(p => p.Equals("/registersystemrequest/accesspackages")));
            Assert.Equal("One or all the accesspackage(s) is not found in altinn's access packages or is not delegable because they are not part of REGN/REVI/Forretningsfører roller", error.Detail);
        }

        [Fact]
        public async Task SystemRegister_Create_DuplicateResource_BadRequest()
        {
            // Arrange
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterDuplicateResource.json";

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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

            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Success_Admin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Success_Owner_NotAdmin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            }
        }

        [Fact]
        public async Task SystemRegister_Update_AccessPackage_Forbid_NotOwner_NotAdmin()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegisterWithoutRight.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);
            string dataFileName01 = "Data/SystemRegister/Json/SystemRegister01.json";
            HttpResponseMessage response01 = await CreateSystemRegister(dataFileName01);
            string dataFileName02 = "Data/SystemRegister/Json/SystemRegisterWithAccessPackageNull.json";
            HttpResponseMessage response02 = await CreateSystemRegister(dataFileName02);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response01.StatusCode == System.Net.HttpStatusCode.OK && response02.StatusCode == System.Net.HttpStatusCode.OK)
            {
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister");
            HttpResponseMessage getAllResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<RegisteredSystemResponse> list = JsonSerializer.Deserialize<List<RegisteredSystemResponse>>(await getAllResponse.Content.ReadAsStringAsync(), _options);
            Assert.True(list.Count == 0);
        }

        [Fact]
        public async Task SystemRegister_Get()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            var expectedRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:resource", Value = "app_ttd_endring-av-navn-v2" }
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            var expectedRights = new List<AttributePair>
            {
                new AttributePair { Id = "urn:altinn:org", Value = "ttd" },
                new AttributePair { Id = "urn:altinn:app", Value = "endring-av-navn-v2" },
            };

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_system_with_app";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

                HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/{name}/accesspackages");
                HttpResponseMessage responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                List<AccessPackage> list = JsonSerializer.Deserialize<List<AccessPackage>>(await responseMessage.Content.ReadAsStringAsync(), _options);
                Assert.Equal("urn:altinn:accesspackage:skattnaering", list[0].Urn);
                Assert.Single(list);
            }
        }

        [Fact]
        public async Task SystemRegister_Get_AccessPackages_NoAccessPackages()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string name = "991825827_the_matrix";
                HttpClient client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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

                Stream dataStream = File.OpenRead("Data/SystemRegister/Json/SystemRegisterUpdateRequest.json");
                StreamContent content = new StreamContent(dataStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpRequestMessage request = new(HttpMethod.Put, $"/authentication/api/v1/systemregister/vendor/{systemId}/");
                request.Content = content;
                HttpResponseMessage updateResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
                HttpResponseMessage getResponse = await GetSystemRegister(systemId);
                RegisteredSystemResponse actualRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(await getResponse.Content.ReadAsStringAsync(), _options);
                string systemRegister = File.OpenText("Data/SystemRegister/Json/SystemRegisterUpdateResponse.json").ReadToEnd();
                RegisteredSystemResponse expectedRegisteredSystem = JsonSerializer.Deserialize<RegisteredSystemResponse>(systemRegister, options);
                AssertionUtil.AssertRegisteredSystem(expectedRegisteredSystem, actualRegisteredSystem);
                Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            }
        }

        [Fact]
        public async Task SystemRegister_Update_System_BadRequest()
        {
            string dataFileName = "Data/SystemRegister/Json/SystemRegister.json";
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            HttpResponseMessage response = await CreateSystemRegister(dataFileName);

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
            Assert.Equal("One or all the accesspackage(s) is not found in altinn's access packages or is not delegable because they are not part of REGN/REVI/Forretningsfører roller", error.Detail);
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
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/vendor/{systemId}");
            HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            
            return getResponse;
        }
    }
}
