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
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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
            services.AddSingleton<ISystemUserService, SystemUserServiceMock>();
            services.AddSingleton<ISystemRegisterService, SystemRegisterServiceMock>();
            SetupDateTimeMock();
            SetupGuidMock();
        }
    
        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsListOK()
        {
            HttpClient client = CreateClient();  //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count > 0);
        }

        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 0;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsOK()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
 
            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response3.StatusCode);
            Assert.True(systemUserDoesExist is not null);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);
            var id = list[0].Id;
            string para = $"{partyId}/123456778890";

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response3.StatusCode);
            Assert.False(response3.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsOk()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeDeleted = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.True(shouldBeDeleted.IsDeleted);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/1/1234567890");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.False(response2.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Update_ReturnsOk()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), _options);

            SystemUserUpdateDto dto = new() 
                {
                    Id = list[0].Id,
                    OwnedByPartyId = partyId.ToString(),                     
                    IntegrationTitle = list[0].IntegrationTitle, 
                    ProductName = list[0].SystemName
                };

            string para = $"{partyId}/{list[0].Id}";
            
            dto.ProductName = "updated_product_name";

            HttpRequestMessage request2 = new(HttpMethod.Put, $"/authentication/api/v1/systemuser");
            request2.Content = JsonContent.Create<SystemUserUpdateDto>(dto, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeUpdated = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.Equal("updated_product_name", shouldBeUpdated!.SystemName);
        }

        [Fact]
        public async Task SystemUser_Update_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            SystemUser doesNotExist = new() { Id = "123" };

            HttpRequestMessage request2 = new(HttpMethod.Put, $"/authentication/api/v1/systemuser")
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
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                PartyId = partyId,
                IntegrationTitle = "IntegrationTitleValue",
                ProductName = "ProductNameValue"
            };

            HttpRequestMessage request2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{partyId}");
            request2.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
                         
            SystemUser shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await response2.Content.ReadAsStringAsync(), _options);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, (int)response2.StatusCode);
            Assert.Equal("IntegrationTitleValue", shouldBeCreated.IntegrationTitle);            
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsNotFound()
        {
            HttpClient client = CreateClient(); //GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUser newSystemUser = new SystemUser
            {
                SystemName = "This is the new SystemUser!",
                Id = "12334523456346"
            };

            HttpRequestMessage request2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(newSystemUser, new MediaTypeHeaderValue("application/json"), _options);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response3.StatusCode);
            Assert.Equal(StatusCodes.Status404NotFound, (int)response3.StatusCode);
            Assert.False(response3.IsSuccessStatusCode);
        }

        //private HttpClient GetTestClient(
        //  ISblCookieDecryptionService cookieDecryptionService,
        //  IUserProfileService userProfileService,
        //  bool enableOidc = false,
        //  bool forceOidc = false,
        //  string defaultOidc = "altinn")
        //{
        //    HttpClient client = _factory.WithWebHostBuilder(builder =>
        //    {
        //        string configPath = GetConfigPath();
        //        builder.ConfigureAppConfiguration((context, conf) =>
        //        {
        //            conf.AddJsonFile(configPath);
        //        });

        //        var configuration = new ConfigurationBuilder()
        //          .AddJsonFile(configPath)
        //          .Build();

        //        configuration.GetSection("GeneralSettings:EnableOidc").Value = enableOidc.ToString();
        //        configuration.GetSection("GeneralSettings:ForceOidc").Value = forceOidc.ToString();
        //        configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = defaultOidc;

        //        IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");
               
        //        builder.ConfigureTestServices(services =>
        //        {
        //            services.Configure<GeneralSettings>(generalSettingSection);
        //            services.AddSingleton(cookieDecryptionService);
        //            services.AddSingleton(userProfileService);
        //            services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
        //            services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
        //            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
        //            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        //            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
        //            services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
        //            services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
        //            services.AddSingleton<ISystemUserService, SystemUserServiceMock>();
        //            services.AddSingleton(new Mock<ISystemClock>());                                        
        //            services.AddSingleton(new Mock<IGuidService>());
        //        });
        //    }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        //    return client;
        //}

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
    }
}
