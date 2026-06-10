#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for self identified authentication. This is used for users that are not registered in the system, but still need to be able to authenticate and use the system. The controller should have methods that validates the credentials of the user and returns a token that can be used for subsequent requests.
    /// </summary>
    [Route("authentication/api/v1/enduser/selfidentified")]
    [ApiController]
    public class SelfIdentifiedAuthenticationController(
        IUserProfileService profileService,
        ISelfIdentifiedLinkService linkService,
        ISelfIdentifiedLinkTokenService linkTokenService) : ControllerBase
    {
        private readonly IUserProfileService _profileService = profileService;
        private readonly ISelfIdentifiedLinkService _linkService = linkService;
        private readonly ISelfIdentifiedLinkTokenService _linkTokenService = linkTokenService;

        /// <summary>
        /// Validates username and password for a self identified user. The credentials are verified against the
        /// SBL Bridge Profile API and, when valid, the party UUID of the resolved user is returned.
        /// </summary>
        [HttpPost("validate-credentials")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> ValidateCredentials([FromBody] SiUserCredentials credentials)
        {
            UserCredentialVerificationResult result =
                await _profileService.ValidateCredentialsAsync(credentials.UserName, credentials.Password);

            if (result.IsLocked)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Account is temporarily locked." });
            }

            if (result.WrongUserType)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "User is not a self identified user." });
            }

            if (result.UserProfile?.Party?.PartyUuid is { } partyUuid)
            {
                return Ok(partyUuid);
            }

            return Unauthorized(new { Message = "Invalid credentials." });
        }

        /// <summary>
        /// Requests an account-link email for a self-identified user (forgot-password / account-claim
        /// flow, issue #2035). Looks up the SI user by username and, when it exists with a stored email,
        /// sends a link (carrying a short-lived token binding the SI user to the authenticated person)
        /// to that email. Returns the masked recipient address so the caller can show where the email
        /// went (e.g. "Email sent to r*****@g****.com"); <see cref="SelfIdentifiedLinkResponse.MaskedEmail"/>
        /// is <c>null</c> when no email was sent.
        /// </summary>
        [HttpPost("link-request")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> RequestLink([FromBody] SelfIdentifiedLinkRequest request, CancellationToken cancellationToken)
        {
            Guid toPartyUuid = AuthenticationHelper.GetPartyUuId(HttpContext);
            if (toPartyUuid == Guid.Empty)
            {
                // The authenticated caller has no party UUID claim, so there is no usable 'to' party to
                // bind the link to. This is a precondition failure, not a property of the SI username.
                return BadRequest(new { Message = "Authenticated user has no party UUID." });
            }

            string? maskedEmail = await _linkService.RequestLinkAsync(request.UserName, toPartyUuid, cancellationToken);

            return Ok(new SelfIdentifiedLinkResponse { MaskedEmail = maskedEmail });
        }

        /// <summary>
        /// Redeems a self-identified account-link token from the email (issue #2035). Validates the
        /// token and enforces that the authenticated caller is the same person who requested the link
        /// (<c>to_party_uuid</c>). On success returns the SI account's party UUID (<c>from_party_uuid</c>)
        /// - the same shape as <c>validate-credentials</c> - so access-management reuses its existing
        /// connection-creation step unchanged.
        /// </summary>
        [HttpPost("validate-link-token")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> ValidateLinkToken([FromBody] SelfIdentifiedLinkTokenRequest request, CancellationToken cancellationToken)
        {
            Guid callerPartyUuid = AuthenticationHelper.GetPartyUuId(HttpContext);
            if (callerPartyUuid == Guid.Empty)
            {
                return BadRequest(new { Message = "Authenticated user has no party UUID." });
            }

            SelfIdentifiedLinkTokenResult result = await _linkTokenService.ValidateAsync(request.Token, cancellationToken);

            if (!result.IsValid)
            {
                return Unauthorized(new { Message = "Invalid or expired link token." });
            }

            // Requester == consumer: the link may only be redeemed by the same person who requested it.
            if (result.ToPartyUuid != callerPartyUuid)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Link token does not belong to the authenticated user." });
            }

            // Single-use (jti) enforcement is not yet implemented - tracked as an open item in #2035.
            return Ok(result.FromPartyUuid);
        }
    }
}
