#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SelfIdentifiedLinkTokenService"/> - the mint/validate of the
/// self-identified account-link token (issue #2035). Uses an in-memory self-signed certificate and a
/// <see cref="FakeTimeProvider"/>; no external dependencies.
/// </summary>
public class SelfIdentifiedLinkTokenServiceTests
{
    private const int SourceUserId = 1337;
    private static readonly Guid TargetPartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");

    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly SelfIdentifiedLinkTokenSettings _settings = new()
    {
        Issuer = "https://test/selfidentified-link",
        Audience = "test:selfidentified-link",
        LifetimeMinutes = 15,
        ClockSkewSeconds = 30,
    };

    [Fact]
    public async Task MintThenValidate_RoundTrips_SourceAndTarget()
    {
        SelfIdentifiedLinkTokenService service = CreateService();

        string token = await service.MintAsync(SourceUserId, TargetPartyUuid);
        SelfIdentifiedLinkTokenResult result = await service.ValidateAsync(token);

        Assert.True(result.IsValid);
        Assert.Equal(SourceUserId, result.SourceUserId);
        Assert.Equal(TargetPartyUuid, result.TargetPartyUuid);
        Assert.False(string.IsNullOrEmpty(result.TokenId));
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Validate_EmptyToken_IsInvalid()
    {
        SelfIdentifiedLinkTokenResult result = await CreateService().ValidateAsync("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_MalformedToken_IsInvalid()
    {
        SelfIdentifiedLinkTokenResult result = await CreateService().ValidateAsync("not-a-jwt");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ExpiredToken_IsInvalid()
    {
        SelfIdentifiedLinkTokenService service = CreateService();
        string token = await service.MintAsync(SourceUserId, TargetPartyUuid);

        // Advance past lifetime + clock skew.
        _timeProvider.Advance(TimeSpan.FromMinutes(_settings.LifetimeMinutes) + TimeSpan.FromSeconds(_settings.ClockSkewSeconds + 1));

        SelfIdentifiedLinkTokenResult result = await service.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_WithinLifetime_IsValid()
    {
        SelfIdentifiedLinkTokenService service = CreateService();
        string token = await service.MintAsync(SourceUserId, TargetPartyUuid);

        _timeProvider.Advance(TimeSpan.FromMinutes(_settings.LifetimeMinutes - 1));

        SelfIdentifiedLinkTokenResult result = await service.ValidateAsync(token);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WrongAudience_IsInvalid()
    {
        // Token minted with a different audience must be rejected by the configured validator.
        X509Certificate2 cert = CreateSelfSignedCertificate();
        SelfIdentifiedLinkTokenService minting = CreateService(cert, new SelfIdentifiedLinkTokenSettings
        {
            Issuer = _settings.Issuer,
            Audience = "some-other-audience",
            LifetimeMinutes = _settings.LifetimeMinutes,
            ClockSkewSeconds = _settings.ClockSkewSeconds,
        });
        SelfIdentifiedLinkTokenService validating = CreateService(cert, _settings);

        string token = await minting.MintAsync(SourceUserId, TargetPartyUuid);
        SelfIdentifiedLinkTokenResult result = await validating.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_WrongIssuer_IsInvalid()
    {
        X509Certificate2 cert = CreateSelfSignedCertificate();
        SelfIdentifiedLinkTokenService minting = CreateService(cert, new SelfIdentifiedLinkTokenSettings
        {
            Issuer = "https://some-other-issuer",
            Audience = _settings.Audience,
            LifetimeMinutes = _settings.LifetimeMinutes,
            ClockSkewSeconds = _settings.ClockSkewSeconds,
        });
        SelfIdentifiedLinkTokenService validating = CreateService(cert, _settings);

        string token = await minting.MintAsync(SourceUserId, TargetPartyUuid);
        SelfIdentifiedLinkTokenResult result = await validating.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_SignedWithDifferentKey_IsInvalid()
    {
        // Simulates a token signed by a different certificate (e.g. an auth/OIDC key) - must fail
        // signature validation against the link-token cert.
        SelfIdentifiedLinkTokenService minting = CreateService(CreateSelfSignedCertificate(), _settings);
        SelfIdentifiedLinkTokenService validating = CreateService(CreateSelfSignedCertificate(), _settings);

        string token = await minting.MintAsync(SourceUserId, TargetPartyUuid);
        SelfIdentifiedLinkTokenResult result = await validating.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    private SelfIdentifiedLinkTokenService CreateService() =>
        CreateService(CreateSelfSignedCertificate(), _settings);

    private SelfIdentifiedLinkTokenService CreateService(X509Certificate2 certificate, SelfIdentifiedLinkTokenSettings settings) =>
        new(
            new StubCertificateProvider(certificate),
            Options.Create(settings),
            _timeProvider,
            NullLogger<SelfIdentifiedLinkTokenService>.Instance);

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=si-link-token-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private sealed class StubCertificateProvider : ISelfIdentifiedLinkTokenCertificateProvider
    {
        private readonly List<X509Certificate2> _certificates;

        public StubCertificateProvider(X509Certificate2 certificate) => _certificates = [certificate];

        public Task<List<X509Certificate2>> GetCertificates() => Task.FromResult(_certificates);
    }
}
