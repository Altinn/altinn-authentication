#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers;

/// <summary>
/// Unit tests for the <c>redeem-link</c> action on <see cref="SelfIdentifiedAuthenticationController"/>
/// (issue #2035): token validation, the requester==consumer check, the direct call to access-management
/// to create the connection, and the returned <c>from_party_uuid</c>. Constructs the controller directly
/// with mocks and a claims principal - no database/Docker.
/// </summary>
public class SelfIdentifiedRedeemLinkTests
{
    private static readonly Guid FromPartyUuid = Guid.Parse("2c3bb12a-5e41-4cc9-9a36-7b5ac6f9f102");
    private static readonly Guid CallerPartyUuid = Guid.Parse("9f1d6e0b-3a72-4f1e-8c44-1b2c3d4e5f60");
    private const string Token = "the.link.token";

    private readonly Mock<ISelfIdentifiedLinkTokenService> _tokenService = new();
    private readonly Mock<IAccessManagementClient> _accessManagement = new();

    [Fact]
    public async Task RedeemLink_ValidAndCallerMatches_CreatesConnectionAndReturnsFromPartyUuid()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Valid(FromPartyUuid, CallerPartyUuid, "jti-1"));
        _accessManagement.Setup(a => a.CreateSelfIdentifiedUserConnection(FromPartyUuid, CallerPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ActionResult result = await CreateController(CallerPartyUuid).RedeemLink(new() { Token = Token }, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(FromPartyUuid, Assert.IsType<Guid>(ok.Value));
        _accessManagement.Verify(a => a.CreateSelfIdentifiedUserConnection(FromPartyUuid, CallerPartyUuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedeemLink_InvalidToken_ReturnsUnauthorized_NoDelegation()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Invalid("bad"));

        ActionResult result = await CreateController(CallerPartyUuid).RedeemLink(new() { Token = Token }, default);

        Assert.IsType<UnauthorizedObjectResult>(result);
        _accessManagement.Verify(a => a.CreateSelfIdentifiedUserConnection(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RedeemLink_CallerIsNotToParty_ReturnsForbidden_NoDelegation()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Valid(FromPartyUuid, Guid.NewGuid(), "jti-1"));

        ActionResult result = await CreateController(CallerPartyUuid).RedeemLink(new() { Token = Token }, default);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        _accessManagement.Verify(a => a.CreateSelfIdentifiedUserConnection(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RedeemLink_DelegationFails_ReturnsBadGateway()
    {
        _tokenService.Setup(t => t.ValidateAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelfIdentifiedLinkTokenResult.Valid(FromPartyUuid, CallerPartyUuid, "jti-1"));
        _accessManagement.Setup(a => a.CreateSelfIdentifiedUserConnection(FromPartyUuid, CallerPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ActionResult result = await CreateController(CallerPartyUuid).RedeemLink(new() { Token = Token }, default);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, obj.StatusCode);
    }

    [Fact]
    public async Task RedeemLink_CallerHasNoPartyUuid_ReturnsBadRequest()
    {
        ActionResult result = await CreateController(Guid.Empty).RedeemLink(new() { Token = Token }, default);

        Assert.IsType<BadRequestObjectResult>(result);
        _tokenService.Verify(t => t.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private SelfIdentifiedAuthenticationController CreateController(Guid callerPartyUuid)
    {
        var controller = new SelfIdentifiedAuthenticationController(
            Mock.Of<IUserProfileService>(),
            Mock.Of<ISelfIdentifiedLinkService>(),
            _tokenService.Object,
            _accessManagement.Object);

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
