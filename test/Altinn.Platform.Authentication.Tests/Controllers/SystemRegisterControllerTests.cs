using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authentication.Controllers;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using AltinnCore.Authentication.JwtCookie;
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
    public class SystemRegisterControllerTests :IClassFixture<WebApplicationFactory<SystemRegisterController>>
    {
        private readonly WebApplicationFactory<SystemRegisterController> _factory;
        private readonly Mock<ISystemRegisterService> _systemRegisterService;
        private readonly Mock<IUserProfileService> _userProfileService;
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService;
        private readonly JsonSerializerOptions jsonOptions = new() 
        {
            PropertyNameCaseInsensitive = true,
        };

        public SystemRegisterControllerTests(
                WebApplicationFactory<SystemRegisterController> factory)
        {
            _factory = factory;
            _systemRegisterService = new Mock<ISystemRegisterService>();
            _userProfileService = new Mock<IUserProfileService>();
            _sblCookieDecryptionService = new Mock<ISblCookieDecryptionService>();
        }

        [Fact]
        public async Task SystemRegister_Get_ListofAll()
        {
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);            
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead );
            List<RegisteredSystem> list = JsonSerializer.Deserialize<List<RegisteredSystem>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            Assert.True(list.Count > 0);
        }

        [Fact]
        public async Task SystemRegister_Get_ProductDefaultRights()
        {
            string name = "Awesome";
            HttpClient client = GetTestClient(_sblCookieDecryptionService.Object, _userProfileService.Object);
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemregister/product/{name}");
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            List<DefaultRights> list = JsonSerializer.Deserialize<List<DefaultRights>>(await response.Content.ReadAsStringAsync(), jsonOptions);
            Assert.Equal("Skatteetaten", list[0].ServiceProvider);
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

                IConfigurationRoot configuration = new ConfigurationBuilder()
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
                    services.AddSingleton<ISystemUserService, SystemUserServiceMock>();                    
                    services.AddSingleton<IUserProfileService, UserProfileService>();
                    services.AddSingleton<ISystemRegisterService, SystemRegisterServiceMock>();

                    if (featureManager is not null)
                    {
                        services.AddSingleton(featureManager);
                    }
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            return client;
        }

        private static string GetConfigPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SystemRegisterControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, $"../../../appsettings.json");
        }
    }
}
