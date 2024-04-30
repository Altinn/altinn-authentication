﻿using System;
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
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Authentication;
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
    public class SystemUserControllerTest :IClassFixture<WebApplicationFactory<SystemUserController>>
    {
        private readonly WebApplicationFactory<SystemUserController> _factory;
        private readonly Mock<ISystemUserService> _systemUserService;
        private readonly Mock<IUserProfileService> _userProfileService;
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public SystemUserControllerTest(WebApplicationFactory<SystemUserController> factory)
        {
            _factory = factory;
            _systemUserService = new Mock<ISystemUserService>();
            _userProfileService = new Mock<IUserProfileService>();
            _sblCookieDecryptionService = new Mock<ISblCookieDecryptionService>();
        }

        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsListOK()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(list is not null);
            Assert.True(list.Count > 0);
        }

        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
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
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
 
            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response3.StatusCode);
            Assert.True(systemUserDoesExist is not null);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
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
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
            HttpRequestMessage request2 = new(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeDeleted = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.True(shouldBeDeleted.IsDeleted);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
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
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);

            SystemUserUpdateDto dto = new() 
                {
                    Id = list[0].Id,
                    OwnedByPartyId = partyId.ToString(),                     
                    IntegrationTitle = list[0].IntegrationTitle, 
                    ProductName = list[0].ProductName 
                };

            string para = $"{partyId}/{list[0].Id}";
            
            dto.ProductName = "updated_product_name";

            HttpRequestMessage request2 = new(HttpMethod.Put, $"/authentication/api/v1/systemuser");
            request2.Content = JsonContent.Create<SystemUserUpdateDto>(dto, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
            
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeUpdated = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.Equal("updated_product_name", shouldBeUpdated.ProductName);
        }

        [Fact]
        public async Task SystemUser_Update_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            SystemUser doesNotExist = new() { Id = "123" };

            HttpRequestMessage request2 = new(HttpMethod.Put, $"/authentication/api/v1/systemuser")
            {
                Content = JsonContent.Create<SystemUser>(doesNotExist, new MediaTypeHeaderValue("application/json"), jsonOptions)
            };
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.False(response2.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsOk()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUserRequestDto newSystemUser = new()
            {
                PartyId = partyId.ToString(),
                IntegrationTitle = "IntegrationTitleValue",
                ProductName = "ProductNameValue"
            };

            HttpRequestMessage request2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser");
            request2.Content = JsonContent.Create<SystemUserRequestDto>(newSystemUser, new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);
                         
            SystemUser shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await response2.Content.ReadAsStringAsync(), jsonOptions);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response2.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, (int)response2.StatusCode);
            Assert.Equal("IntegrationTitleValue", shouldBeCreated.IntegrationTitle);            
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenUtil.GetTestToken());
            client.DefaultRequestHeaders.Add("X-Altinn-EnterpriseUser-Authentication", "VmFsaWRVc2VyOlZhbGlkUGFzc3dvcmQ=");

            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUser newSystemUser = new SystemUser
            {
                ProductName = "This is the new SystemUser!",
                Id = "12334523456346"
            };

            HttpRequestMessage request2 = new(HttpMethod.Post, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(newSystemUser, new MediaTypeHeaderValue("application/json"), jsonOptions);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response3.StatusCode);
            Assert.Equal(StatusCodes.Status404NotFound, (int)response3.StatusCode);
            Assert.False(response3.IsSuccessStatusCode);
        }

        private HttpClient GetTestClient(
          ISblCookieDecryptionService cookieDecryptionService,
          IUserProfileService userProfileService,
          bool enableOidc = false,
          bool forceOidc = false,
          string defaultOidc = "altinn")
        {
            HttpClient client = _factory.WithWebHostBuilder(builder =>
            {
                string configPath = GetConfigPath();
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
               
                builder.ConfigureTestServices(services =>
                {
                    services.Configure<GeneralSettings>(generalSettingSection);
                    services.AddSingleton(cookieDecryptionService);
                    services.AddSingleton(userProfileService);
                    services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
                    services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
                    services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
                    services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
                    services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
                    services.AddSingleton<ISystemUserService, SystemUserServiceMock>();
                    services.AddSingleton(new Mock<ISystemClock>());                                        
                    services.AddSingleton(new Mock<IGuidService>());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            return client;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SystemUserControllerTest).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }

        private static AuthenticationEvent GetAuthenticationEvent(AuthenticationMethod authMethod, SecurityLevel authLevel, int? orgNumber, AuthenticationEventType authEventType, int? userId = null, bool isAuthenticated = true, string? externalSessionId = null)
        {
            AuthenticationEvent authenticationEvent = new AuthenticationEvent();
            authenticationEvent.Created = new DateTime(2018, 05, 15, 02, 05, 00);
            authenticationEvent.AuthenticationMethod = authMethod;
            authenticationEvent.AuthenticationLevel = authLevel;
            authenticationEvent.OrgNumber = orgNumber;
            authenticationEvent.EventType = authEventType;
            authenticationEvent.UserId = userId;
            authenticationEvent.IsAuthenticated = isAuthenticated;
            authenticationEvent.SessionId = "eaec330c-1e2d-4acb-8975-5f3eba12b2fb";
            authenticationEvent.ExternalSessionId = externalSessionId;

            return authenticationEvent;
        }
    }
}
