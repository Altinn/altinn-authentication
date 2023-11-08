﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Register.Models;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
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
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);

            Assert.True(list is not null);
            Assert.True(list.Count > 0);
        }

        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 0;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            Assert.True(!response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsOK()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
 
            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser systemUserDoesExist = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);
            
            Assert.True(systemUserDoesExist is not null);
        }

        [Fact]
        public async Task SystemUser_Get_Single_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/123456778890";

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.True(!response3.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsOk()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeDeleted = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);
            Assert.True(shouldBeDeleted.IsDeleted);
        }

        [Fact]
        public async Task SystemUser_Delete_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";
            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Delete, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/1/1234567890");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.True(!response3.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Update_ReturnsOk()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";

            list[0].Description = "Hey there!";
            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Put, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(list[0], new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"), jsonOptions);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeUpdated = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);
            Assert.Equal("Hey there!", shouldBeUpdated.Description);
        }

        [Fact]
        public async Task SystemUser_Update_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{partyId}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<SystemUser> list = JsonSerializer.Deserialize<List<SystemUser>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            var id = list[0].Id;
            string para = $"{partyId}/{id}";

            list[0].Description = "Hey there!";
            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Put, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(list[0], new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"), jsonOptions);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/1/122323453456");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.True(!response3.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsOk()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUser newSystemUser = new SystemUser
            {
                Description = "This is the new SystemUser!",
                Id = id.ToString()
            };

            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Post, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(newSystemUser, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"), jsonOptions);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);
            SystemUser shouldBeCreated = JsonSerializer.Deserialize<SystemUser>(await response3.Content.ReadAsStringAsync(), jsonOptions);
            Assert.Equal("This is the new SystemUser!", shouldBeCreated.Description);            
        }

        [Fact]
        public async Task SystemUser_Create_ReturnsNotFound()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            int partyId = 1;
            Guid id = Guid.NewGuid();

            string para = $"{partyId}/{id}";
            SystemUser newSystemUser = new SystemUser
            {
                Description = "This is the new SystemUser!",
                Id = "12334523456346"
            };

            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Post, $"/authentication/api/v1/systemuser/{para}");
            request2.Content = JsonContent.Create<SystemUser>(newSystemUser, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"), jsonOptions);
            HttpResponseMessage response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseContentRead);

            HttpRequestMessage request3 = new HttpRequestMessage(HttpMethod.Get, $"/authentication/api/v1/systemuser/{para}");
            HttpResponseMessage response3 = await client.SendAsync(request3, HttpCompletionOption.ResponseContentRead);

            Assert.True(!response3.IsSuccessStatusCode);
        }

        private HttpClient GetTestClient(ISblCookieDecryptionService sblCookieDecryptionService, IUserProfileService userProfileService, IFeatureManager featureManager = null)
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

                configuration.GetSection("GeneralSettings:EnableOidc").Value = "false";
                configuration.GetSection("GeneralSettings:ForceOidc").Value = "false";
                configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = "Altinn";

                IConfigurationSection generalSettingsSection = configuration.GetSection("GeneralSettings");

                builder.ConfigureTestServices(services =>
                {
                    services.Configure<GeneralSettings>(generalSettingsSection);
                    services.AddSingleton(sblCookieDecryptionService);
                    services.AddSingleton(userProfileService);
                    services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
                    services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
                    services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
                    services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
                    services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
                    services.AddSingleton<ISystemUserService, SystemUserService>();
                    services.AddSingleton<IUserProfileService, UserProfileService>();
                    if (featureManager is not null)
                    {
                        services.AddSingleton(featureManager);
                    }
                });

            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false});

            return client;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SystemUserControllerTest).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }
    }
}
