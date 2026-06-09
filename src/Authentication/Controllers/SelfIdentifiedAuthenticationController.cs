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
        ISelfIdentifiedLinkService linkService) : ControllerBase
    {
        private readonly IUserProfileService _profileService = profileService;
        private readonly ISelfIdentifiedLinkService _linkService = linkService;

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
        /// to that email. Always responds <c>202 Accepted</c> regardless of whether the user exists, so
        /// the username cannot be enumerated.
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

            await _linkService.RequestLinkAsync(request.UserName, toPartyUuid, cancellationToken);

            return Accepted();
        }
    }
}
