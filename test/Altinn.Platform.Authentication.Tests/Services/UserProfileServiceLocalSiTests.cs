#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services;

/// <summary>
/// Unit tests for the local self-identified credential validation path in
/// <see cref="UserProfileService"/> (feature flag
/// <see cref="FeatureFlags.LocalSelfIdentifiedCredentialValidation"/> enabled). These tests do not
/// require Docker - they exercise the service directly with mocks and a throwing HTTP handler that
/// guarantees the SBL Bridge is never contacted on the local path.
/// </summary>
public class UserProfileServiceLocalSiTests
{
    // Independently computed vector: Base64( SHA1( UTF8("P@ssw0rd!") || Base64Decode(salt) ) ).
    private const string Password = "P@ssw0rd!";
    private const string Salt = "AQIDBAUGBwgJCgsM";
    private const string PasswordHash = "YwOo/PvLMkNE37TEnEo3xxQs/qk=";

    private static readonly Guid PartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");

    private readonly Mock<ISelfIdentifiedUserCredentialRepository> _repo = new();
    private readonly Mock<IFeatureManager> _featureManager = new();

    public UserProfileServiceLocalSiTests()
    {
        _featureManager
            .Setup(f => f.IsEnabledAsync(FeatureFlags.LocalSelfIdentifiedCredentialValidation))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ValidateCredentials_Local_ValidCredentials_ReturnsProfileWithPartyUuid()
    {
        _repo.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential());

        UserCredentialVerificationResult result = await CreateService().ValidateCredentialsAsync("user", Password);

        Assert.False(result.IsLocked);
        Assert.False(result.WrongUserType);
        Assert.NotNull(result.UserProfile);
        Assert.Equal(PartyUuid, result.UserProfile!.Party.PartyUuid);
        Assert.Equal(PartyUuid, result.UserProfile.UserUuid);
        Assert.Equal(UserType.SelfIdentified, result.UserProfile.UserType);
        Assert.Equal(1337, result.UserProfile.UserId);
    }

    [Fact]
    public async Task ValidateCredentials_Local_WrongPassword_ReturnsEmptyResult()
    {
        _repo.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential());

        UserCredentialVerificationResult result = await CreateService().ValidateCredentialsAsync("user", "wrong-password");

        Assert.Null(result.UserProfile);
        Assert.False(result.IsLocked);
    }

    [Fact]
    public async Task ValidateCredentials_Local_InactiveUser_ReturnsEmptyResult()
    {
        SelfIdentifiedUserCredential credential = Credential();
        credential.IsActive = false;
        _repo.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

        UserCredentialVerificationResult result = await CreateService().ValidateCredentialsAsync("user", Password);

        Assert.Null(result.UserProfile);
    }

    [Fact]
    public async Task ValidateCredentials_Local_UnknownUser_ReturnsEmptyResult()
    {
        _repo.Setup(r => r.GetByUsernameAsync("nobody", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SelfIdentifiedUserCredential?)null);

        UserCredentialVerificationResult result = await CreateService().ValidateCredentialsAsync("nobody", Password);

        Assert.Null(result.UserProfile);
    }

    [Fact]
    public async Task ValidateCredentials_Local_EmptyInput_ReturnsEmptyResult()
    {
        UserCredentialVerificationResult result = await CreateService().ValidateCredentialsAsync(string.Empty, string.Empty);

        Assert.Null(result.UserProfile);
        _repo.Verify(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SelfIdentifiedUserCredential Credential() => new()
    {
        PartyUuid = PartyUuid,
        UserId = 1337,
        UserName = "user",
        PasswordHash = PasswordHash,
        Salt = Salt,
        PasswordExpiry = DateTimeOffset.MaxValue,
        IsActive = true
    };

    private UserProfileService CreateService()
    {
        // A handler that throws if invoked - the local path must never call SBL Bridge.
        HttpClient client = new(new ThrowingHandler());
        IOptions<GeneralSettings> settings = Options.Create(new GeneralSettings { OidcRefreshTokenPepper = "unit-test-pepper" });

        return new UserProfileService(
            client,
            settings,
            NullLogger<IUserProfileService>.Instance,
            _featureManager.Object,
            _repo.Object);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP call should not happen on the local validation path.");
    }
}
