#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;

/// <summary>
/// Unit tests for the <c>validate-link-token</c> redemption action on
/// <see cref="SelfIdentifiedAuthenticationController"/> (issue #2035): token validation, the
/// requester==consumer check, and the returned <c>from_party_uuid</c>. Constructs the controller
/// directly with mocks and a claims principal - no database/Docker.
/// </summary>
public class SelfIdentifiedValidateLinkTokenTests
{
    private static readonly Guid FromPartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");
    private static readonly Guid CallerPartyUuid = Guid.Parse("9f1d6e0b-3a72-4f1e-8c44-1b2c3d4e5f60");
    private const string Token = "the.link.token";

    private readonly Mock<ISelfIdentifiedLinkTokenService> _tokenService = new();

    [Fact]
    public async Task ValidateLinkToken_ValidAndCallerMatches_ReturnsFromPartyUuid()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Valid(FromPartyUuid, CallerPartyUuid, "jti-1"));

        ActionResult result = await CreateController(CallerPartyUuid).ValidateLinkToken(new() { Token = Token }, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(FromPartyUuid, Assert.IsType<Guid>(ok.Value));
    }

    [Fact]
    public async Task ValidateLinkToken_InvalidToken_ReturnsUnauthorized()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Invalid("bad"));

        ActionResult result = await CreateController(CallerPartyUuid).ValidateLinkToken(new() { Token = Token }, default);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ValidateLinkToken_CallerIsNotToParty_ReturnsForbidden()
    {
        // Token is valid but bound to a different 'to' party than the authenticated caller.
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Valid(FromPartyUuid, Guid.NewGuid(), "jti-1"));

        ActionResult result = await CreateController(CallerPartyUuid).ValidateLinkToken(new() { Token = Token }, default);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _tokenService.Verify(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateLinkToken_CallerHasNoPartyUuid_ReturnsBadRequest()
    {
        ActionResult result = await CreateController(Guid.Empty).ValidateLinkToken(new() { Token = Token }, default);

        Assert.IsType<BadRequestObjectResult>(result);
        _tokenService.Verify(t => t.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private SelfIdentifiedAuthenticationController CreateController(Guid callerPartyUuid)
    {
        var controller = new SelfIdentifiedAuthenticationController(
            Mock.Of<IUserProfileService>(),
            Mock.Of<ISelfIdentifiedLinkService>(),
            _tokenService.Object);

        List<Claim> claims = [];
        if (callerPartyUuid != Guid.Empty)
        {
            claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, callerPartyUuid.ToString()));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
