#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using Altinn.Register.Contracts.V1;
using AltinnCore.Authentication.Constants;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;

public class SelfIdentifiedAuthenticationControllerTests(
    DbFixture dbFixture,
    WebApplicationFixture webApplicationFixture)
    : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private static readonly DateTimeOffset TestTime = new(2025, 05, 15, 02, 05, 00, TimeSpan.Zero);
    private readonly Mock<IUserProfileService> _userProfileService = new();
    private readonly FakeTimeProvider _timeProviderMock = new();
    private IConfiguration _configuration = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        string configPath = GetConfigPath();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath)
            .Build();

        IConfigurationSection generalSettingSection = configuration.GetSection("GeneralSettings");

        services.Configure<GeneralSettings>(generalSettingSection);
        services.AddSingleton(_userProfileService.Object);
        services.AddSingleton<IAccessManagementClient, AccessManagementClientMock>();
        services.AddSingleton<IOrganisationsService, OrganisationsServiceMock>();
        services.AddSingleton<ISigningKeysRetriever, SigningKeysRetrieverStub>();
        services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
        services.AddSingleton<IEnterpriseUserAuthenticationService, EnterpriseUserAuthenticationServiceMock>();
        services.AddSingleton<IOidcProvider, OidcProviderServiceMock>();
        services.AddSingleton((TimeProvider)_timeProviderMock);

        _configuration = configuration;
        SetupDateTimeMock();
    }

    [Fact]
    public async Task LinkAccount_ReturnsOk_WhenUserProfileFound()
    {
        var expectedPartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");

        _userProfileService
            .Setup(s => s.ValidateCredentialsAsync("user", "pass"))
            .ReturnsAsync(new UserCredentialVerificationResult
            {
                UserProfile = new UserProfile
                {
                    UserName = "user",
                    UserType = UserType.SelfIdentified,
                    Party = new Party { PartyUuid = expectedPartyUuid }
                }
            });

        HttpResponseMessage response = await PostCredentials("user", "pass");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Guid? partyUuid = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.Equal(expectedPartyUuid, partyUuid);
    }

    [Fact]
    public async Task LinkAccount_ReturnsUnauthorized_WhenUserProfileMissing()
    {
        _userProfileService
            .Setup(s => s.ValidateCredentialsAsync("user", "badpass"))
            .ReturnsAsync(new UserCredentialVerificationResult());

        HttpResponseMessage response = await PostCredentials("user", "badpass");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LinkAccount_ReturnsTooManyRequests_WhenAccountIsLocked()
    {
        _userProfileService
            .Setup(s => s.ValidateCredentialsAsync("user", "pass"))
            .ReturnsAsync(new UserCredentialVerificationResult { IsLocked = true });

        HttpResponseMessage response = await PostCredentials("user", "pass");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task LinkAccount_ReturnsForbidden_WhenWrongUserType()
    {
        _userProfileService
            .Setup(s => s.ValidateCredentialsAsync("user", "pass"))
            .ReturnsAsync(new UserCredentialVerificationResult { WrongUserType = true });

        HttpResponseMessage response = await PostCredentials("user", "pass");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostCredentials(string userName, string password)
    {
        // The authenticated caller's party UUID is the connection 'to' party; the endpoint now creates
        // the connection directly, so the token must carry it.
        List<Claim> claims = [new Claim(AltinnCoreClaimTypes.PartyUUID, Guid.NewGuid().ToString())];

        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, claims, addPortalScope: true, now: TestTime));

        return await client.PostAsJsonAsync(
            "/authentication/api/v1/enduser/selfidentified/link",
            new SiUserCredentials { UserName = userName, Password = password });
    }

    private static string GetConfigPath()
    {
        string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(SelfIdentifiedAuthenticationControllerTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder!, "../../../appsettings.test.json");
    }

    private void SetupDateTimeMock()
    {
        _timeProviderMock.SetUtcNow(TestTime);
    }
}
