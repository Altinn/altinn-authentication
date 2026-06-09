#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SelfIdentifiedLinkService"/> - the "request email" orchestration of the
/// self-identified account-link flow (issue #2035).
/// </summary>
public class SelfIdentifiedLinkServiceTests
{
    private const string UserName = "siuser";
    private const string Email = "siuser@example.com";
    private const string Token = "MINTED.TOKEN.VALUE";

    private static readonly Guid FromPartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");
    private static readonly Guid ToPartyUuid = Guid.Parse("9f1d6e0b-3a72-4f1e-8c44-1b2c3d4e5f60");

    private readonly Mock<IUserProfileService> _profile = new();
    private readonly Mock<ISelfIdentifiedLinkTokenService> _tokenService = new();
    private readonly Mock<IAltinnNotificationClient> _notification = new();

    private readonly SelfIdentifiedLinkSettings _settings = new()
    {
        AccessManagementLinkUrl = "https://am.example/accessmanagement/ui/selfidentified-link",
        EmailSubject = "Test subject",
    };

    [Fact]
    public async Task RequestLink_ValidTarget_MintsTokenAndSendsEmailWithLink()
    {
        _profile.Setup(p => p.GetSelfIdentifiedLinkTargetAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfIdentifiedLinkTarget { PartyUuid = FromPartyUuid, Email = Email });
        _tokenService.Setup(t => t.MintAsync(FromPartyUuid, ToPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Token);

        string? sentTo = null;
        string? sentSubject = null;
        string? sentBody = null;
        _notification
            .Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((to, subject, body, _, _) =>
            {
                sentTo = to;
                sentSubject = subject;
                sentBody = body;
            })
            .ReturnsAsync(true);

        await CreateService().RequestLinkAsync(UserName, ToPartyUuid);

        _tokenService.Verify(t => t.MintAsync(FromPartyUuid, ToPartyUuid, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Email, sentTo);
        Assert.Equal("Test subject", sentSubject);
        Assert.NotNull(sentBody);
        Assert.Contains($"{_settings.AccessManagementLinkUrl}?token={Token}", sentBody!);
    }

    [Fact]
    public async Task RequestLink_UnknownTarget_DoesNotMintOrSend()
    {
        _profile.Setup(p => p.GetSelfIdentifiedLinkTargetAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SelfIdentifiedLinkTarget?)null);

        await CreateService().RequestLinkAsync(UserName, ToPartyUuid);

        _tokenService.Verify(
            t => t.MintAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notification.Verify(
            n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SelfIdentifiedLinkService CreateService() =>
        new(
            _profile.Object,
            _tokenService.Object,
            _notification.Object,
            Options.Create(_settings),
            NullLogger<SelfIdentifiedLinkService>.Instance);
}
