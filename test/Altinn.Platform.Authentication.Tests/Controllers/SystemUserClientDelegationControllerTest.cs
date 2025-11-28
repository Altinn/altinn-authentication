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
using Altinn.AccessManagement.Tests.Mocks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Errors;
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
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using App.IntegrationTests.Utils;
using Azure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using static Altinn.Platform.Authentication.Core.Models.SystemUsers.ClientDto;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    public class SystemUserClientDelegationControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private static readonly DateTimeOffset TestTime = new(2025, 05, 15, 02, 05, 00, TimeSpan.Zero);
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

        private readonly Mock<ISystemUserRepository> _systemUserRepository = new();
        private readonly Mock<IUserProfileService> _userProfileService = new();
        private readonly Mock<ISblCookieDecryptionService> _sblCookieDecryptionService = new();

        private readonly FakeTimeProvider timeProviderMock = new();
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

            services.AddSingleton((TimeProvider)timeProviderMock);
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
            SetupSystemUserRepositoryMock_AllSystemUsers();

            timeProviderMock.SetUtcNow(TestTime);
        }

        private static string GetConfigPath()
        {
            string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(AuthenticationControllerTests).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder!, $"../../../appsettings.test.json");
        }

        private void SetupGuidMock()
        {
            guidService.Setup(q => q.NewGuid()).Returns("eaec330c-1e2d-4acb-8975-5f3eba12b2fb");
        }

        private void SetupSystemUserRepositoryMock()
        {
            // Map valid system user IDs to their ReporteeOrgNo
            var systemUserData = new Dictionary<Guid, string>
            {
                { new Guid("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4"), "123456789" },
                { new Guid("fd9d93c7-1dd7-45bc-9772-6ba977b3cd36"), "987654321" },
                { new Guid("d54a721a-b231-4e28-9245-782697ed2bbb"), "555555555" }, // Added for Standard user
                { new Guid("88e6d38a-1b48-46b9-b1cf-ec5ffbe0c144"), "123447789" }, // Invalid partyorgno
                { new Guid("65055192-f4a9-4b47-bc24-46c4b97081c1"), "123357789" }, // Invalid facilitator
            };

            _systemUserRepository
                .Setup(r => r.GetSystemUserById(It.Is<Guid>(id => systemUserData.ContainsKey(id))))
                .ReturnsAsync((Guid id) =>
                {
                    if (id == new Guid("d54a721a-b231-4e28-9245-782697ed2bbb"))
                    {
                        return new SystemUserInternalDTO
                        {
                            Id = id.ToString(),
                            ReporteeOrgNo = systemUserData[id],
                            UserType = SystemUserType.Standard, // Standard type
                            AccessPackages = new List<AccessPackage>
                            {
                                new AccessPackage { Urn = "urn:altinn:accesspackage:skattegrunnlag" }
                            }
                        };
                    }
                    else
                    {
                        return new SystemUserInternalDTO
                        {
                            Id = id.ToString(),
                            ReporteeOrgNo = systemUserData[id],
                            UserType = SystemUserType.Agent,
                            AccessPackages = new List<AccessPackage>
                            {
                                new AccessPackage { Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet" }
                            }
                        };
                    }
                });
        }

        private void SetupSystemUserRepositoryMock_AllSystemUsers()
        {
            var systemUsers = new List<SystemUserInternalDTO>
            {
            new SystemUserInternalDTO
            {
                Id = Guid.NewGuid().ToString(),
                ReporteeOrgNo = "123456789",
                AccessPackages = new List<AccessPackage>
                {
                    new AccessPackage { Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet" }
                }
            },
            new SystemUserInternalDTO
            {
                Id = Guid.NewGuid().ToString(),
                ReporteeOrgNo = "987654321",
                AccessPackages = new List<AccessPackage>
                {
                    new AccessPackage { Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet" }
                }
            }
            };

            _systemUserRepository
                .Setup(r => r.GetAllActiveAgentSystemUsersForParty(It.IsAny<int>()))
                .ReturnsAsync(systemUsers);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
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
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
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
        public async Task GetAvailableClientsForDelegation_ReturnsBadRequest_SystemUser_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=9a5ef699-84c4-4ac8-a16b-1ee9e32b8cc9");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_SystemUserId_NotFound.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("System user not found", error1.Detail);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ReturnsBadRequest_Missing_AgentParameter()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("The agent query parameter is missing or invalid", error1.Detail);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ReturnsBadRequest_InvalidUserType()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=d54a721a-b231-4e28-9245-782697ed2bbb");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Invalid_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("SystemUser is not a valid system user of type agent", error1.Detail);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ValidRequest_Returns_NoClients()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=fd9d93c7-1dd7-45bc-9772-6ba977b3cd36");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue(
                                                        "Bearer", 
                                                        PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientInfoPaginated<ClientInfo> result = JsonSerializer.Deserialize<ClientInfoPaginated<ClientInfo>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Items.Count() == 0);
            Assert.True(result.Links.Next is null);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_Forbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(2234, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task GetAvailableClientsForDelegation_ReturnsBadRequest_SystemOwner_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/available?agent=88e6d38a-1b48-46b9-b1cf-ec5ffbe0c144");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);            
            Assert.Equal("System Owner not Found", problemDetails.Title);
            Assert.Equal("No associated party information found for systemuser owner 123447789", problemDetails.Detail);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientInfoPaginated<ClientInfo> result = JsonSerializer.Deserialize<ClientInfoPaginated<ClientInfo>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Items.Count() > 0);
            Assert.True(result.Links.Next is null);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ValidRequest_Returns_NoClients()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=fd9d93c7-1dd7-45bc-9772-6ba977b3cd36");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientInfoPaginated<ClientInfo> result = JsonSerializer.Deserialize<ClientInfoPaginated<ClientInfo>>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Items.Count() == 0);
        }

        [Fact]
        public async Task GetClientsDelegatedForASystemUser_ValidRequest_ReturnsForbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
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
        public async Task GetClientsDelegatedToSystemUser_ReturnsBadRequest_SystemUser_Notfound()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=9a5ef699-84c4-4ac8-a16b-1ee9e32b8cc9");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_SystemUserId_NotFound.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("System user not found", error1.Detail);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ReturnsBadRequest_Missing_AgentParameter()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("The agent query parameter is missing or invalid", error1.Detail);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ReturnsBadRequest_InvalidUserType()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=d54a721a-b231-4e28-9245-782697ed2bbb");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, clientListResponse.StatusCode);
            AltinnValidationProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 0);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Invalid_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("SystemUser is not a valid system user of type agent", error1.Detail);
        }

        [Fact]
        public async Task GetClientsDelegatedToSystemUser_ReturnsBadRequest_SystemOwner_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/clients/?agent=88e6d38a-1b48-46b9-b1cf-ec5ffbe0c144");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Equal("System Owner not Found", problemDetails.Title);
            Assert.Equal("No associated party information found for systemuser owner 123447789", problemDetails.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new Guid("f1c7ca59-5bf9-4036-bdb8-15062d992eaa");
            Guid systemUserId = new Guid("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientDelegationResponse clientDelegationResponse = JsonSerializer.Deserialize<ClientDelegationResponse>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(clientDelegationResponse is not null);
            Assert.Equal(clientId, clientDelegationResponse.Client);
            Assert.Equal(systemUserId, clientDelegationResponse.Agent);
        }

        [Fact]
        public async Task AddClientToSystemUser_MissingSystemUserId_ClientId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));

            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 1);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("The agent query parameter is missing or invalid", error1.Detail);

            AltinnValidationError error2 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_ClientParameter.ErrorCode);
            Assert.Equal("?client", error2.Paths.First(p => p.Equals("?client")));
            Assert.Equal("The client query parameter is missing or invalid", error2.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_InValidSystemUserId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("d54a721a-b231-4e28-9245-782697ed2bbb");

            AgentDelegation agentDelegation = new()
            {
                CustomerId = clientId,
                Access = new List<ClientRoleAccessPackages>
                {
                    new ClientRoleAccessPackages
                    {
                        Role = "regnskapsfører",
                        Packages = new[] { "regnskapsforer-med-signeringsrettighet", "regnskapsforer-lonn" }
                    }
                }
            };

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            clientListRequest.Content = new StringContent(
                                    JsonSerializer.Serialize(agentDelegation, _options),
                                    Encoding.UTF8,
                                    "application/json");
            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Invalid_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("SystemUser is not a valid system user of type agent", error.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_SystemUserIdNotFound_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("9a5ef699-84c4-4ac8-a16b-1ee9e32b8cc9");

            AgentDelegation agentDelegation = new()
            {
                CustomerId = clientId,
                Access = new List<ClientRoleAccessPackages>
                {
                    new ClientRoleAccessPackages
                    {
                        Role = "regnskapsfører",
                        Packages = new[] { "regnskapsforer-med-signeringsrettighet", "regnskapsforer-lonn" }
                    }
                }
            };

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            clientListRequest.Content = new StringContent(
                        JsonSerializer.Serialize(agentDelegation, _options),
                        Encoding.UTF8,
                        "application/json");
            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_SystemUserId_NotFound.ErrorCode);
            Assert.Equal("?agent", error.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("System user not found", error.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_ReturnsForbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new("f1c7ca59-5bf9-4036-bdb8-15062d992eaa");
            Guid systemUserId = new("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(2234, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task AddClientToSystemUser_ReturnsBadRequest_SystemOwner_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();
            Guid clientId = new("f1c7ca59-5bf9-4036-bdb8-15062d992eaa");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent=88e6d38a-1b48-46b9-b1cf-ec5ffbe0c144&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Equal("System Owner not Found", problemDetails.Title);
            Assert.Equal("No associated party information found for systemuser owner 123447789", problemDetails.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_Retreiving_ClientInformation_Failed()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new Guid("f1c7ca59-5bf9-4036-bdb8-15062d992eaa");
            Guid systemUserId = new Guid("65055192-f4a9-4b47-bc24-46c4b97081c1");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Equal("Forbidden", problemDetails.Title);
            Assert.Equal("Forbidden", problemDetails.Detail);
        }

        [Fact]
        public async Task AddClientToSystemUser_Client_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new Guid("6a734c3a-c707-4bd4-9491-cf0c4c4a54fd");
            Guid systemUserId = new Guid("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Post, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read, altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Equal("Client not found", problemDetails.Title);
            Assert.Equal("Client with client id 6a734c3a-c707-4bd4-9491-cf0c4c4a54fd not found", problemDetails.Detail);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = new("fd9d93c7-1dd7-45bc-9772-6ba977b3cd36");
            Guid systemUserId = new("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            ClientDelegationResponse clientDelegationResponse = JsonSerializer.Deserialize<ClientDelegationResponse>(await clientListResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, clientListResponse.StatusCode);
            Assert.True(clientDelegationResponse is not null);
            Assert.Equal(clientId, clientDelegationResponse.Client);
            Assert.Equal(systemUserId, clientDelegationResponse.Agent);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_MissingSystemUserId_ClientId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.True(problemDetails.Errors.Count > 1);
            AltinnValidationError error1 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error1.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("The agent query parameter is missing or invalid", error1.Detail);

            AltinnValidationError error2 = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Missing_ClientParameter.ErrorCode);
            Assert.Equal("?client", error2.Paths.First(p => p.Equals("?client")));
            Assert.Equal("The client query parameter is missing or invalid", error2.Detail);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_InValidSystemUserId_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("d54a721a-b231-4e28-9245-782697ed2bbb");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_Invalid_SystemUserId.ErrorCode);
            Assert.Equal("?agent", error.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("SystemUser is not a valid system user of type agent", error.Detail);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_SystemUserIdNotFound_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new Guid("9a5ef699-84c4-4ac8-a16b-1ee9e32b8cc9");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage removeClientResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.BadRequest, removeClientResponse.StatusCode);

            AltinnValidationProblemDetails problemDetails = await removeClientResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            AltinnValidationError error = problemDetails.Errors.First(e => e.ErrorCode == ValidationErrors.SystemUser_SystemUserId_NotFound.ErrorCode);
            Assert.Equal("?agent", error.Paths.First(p => p.Equals("?agent")));
            Assert.Equal("System user not found", error.Detail);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_ReturnsForbidden()
        {
            // Arrange
            HttpClient client = CreateClient();

            Guid clientId = Guid.NewGuid();
            Guid systemUserId = new("b8d4d4d9-680b-4905-90c1-47ac5ff0c0a4");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent={systemUserId}&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(2234, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Forbidden, clientListResponse.StatusCode);
        }

        [Fact]
        public async Task RemoveClientToSystemUser_ReturnsBadRequest_SystemOwner_NotFound()
        {
            // Arrange
            HttpClient client = CreateClient();
            Guid clientId = new("f1c7ca59-5bf9-4036-bdb8-15062d992eaa");

            HttpRequestMessage clientListRequest = new(HttpMethod.Delete, $"/authentication/api/v1/enduser/systemuser/clients/?agent=88e6d38a-1b48-46b9-b1cf-ec5ffbe0c144&client={clientId}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.write", 3, TestTime));
            HttpResponseMessage clientListResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.NotFound, clientListResponse.StatusCode);
            AltinnProblemDetails problemDetails = await clientListResponse.Content.ReadFromJsonAsync<AltinnValidationProblemDetails>();
            Assert.NotNull(problemDetails);
            Assert.Equal("System Owner not Found", problemDetails.Title);
            Assert.Equal("No associated party information found for systemuser owner 123447789", problemDetails.Detail);
        }

        [Fact]
        public async Task GetAllAgentSystemUsersForAParty_ValidRequest_ReturnsOk()
        {
            // Arrange
            HttpClient client = CreateClient();
            string orgnummer = "123456789";
            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/agents?party={orgnummer}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage systemUsersResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);
            List<SystemUserInternalDTO> result = JsonSerializer.Deserialize<List<SystemUserInternalDTO>>(await systemUsersResponse.Content.ReadAsStringAsync(), _options);

            Assert.Equal(HttpStatusCode.OK, systemUsersResponse.StatusCode);
            Assert.True(result is not null);
            Assert.True(result.Count() > 0);
        }

        [Fact]
        public async Task GetAllAgentSystemUsersForAParty_Invalidorgnummer_ReturnsBadRequest()
        {
            // Arrange
            HttpClient client = CreateClient();
            string orgnummer = "123447789";
            HttpRequestMessage clientListRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/agents?party={orgnummer}");
            clientListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetClientDelegationToken(1337, null, "altinn:clientdelegations.read", 3, TestTime));
            HttpResponseMessage systemUsersResponse = await client.SendAsync(clientListRequest, HttpCompletionOption.ResponseContentRead);          

            Assert.Equal(HttpStatusCode.NotFound, systemUsersResponse.StatusCode);
        }

        [Fact]
        public async Task GetAllAgentSystemUsersForAParty_ReturnsUnAuthorized()
        {
            // Arrange
            HttpClient client = CreateClient();
            string orgnummer = "123456789";
            HttpRequestMessage systemUsersRequest = new(HttpMethod.Get, $"/authentication/api/v1/enduser/systemuser/agents?party={orgnummer}");
            HttpResponseMessage systemUsersResponse = await client.SendAsync(systemUsersRequest, HttpCompletionOption.ResponseContentRead);

            Assert.Equal(HttpStatusCode.Unauthorized, systemUsersResponse.StatusCode);
        }
    }
}
