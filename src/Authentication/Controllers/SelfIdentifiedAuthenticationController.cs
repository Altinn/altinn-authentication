#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
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
        ISelfIdentifiedLinkTokenService linkTokenService,
        IAccessManagementClient accessManagementClient) : ControllerBase
    {
        private readonly IUserProfileService _profileService = profileService;
        private readonly ISelfIdentifiedLinkService _linkService = linkService;
        private readonly ISelfIdentifiedLinkTokenService _linkTokenService = linkTokenService;
        private readonly IAccessManagementClient _accessManagementClient = accessManagementClient;

        /// <summary>
        /// Validates username and password for a self identified user. When valid, the connection from
        /// the resolved SI user to the authenticated person is created in access-management **directly**
        /// (rather than returning the party UUID for the BFF to delegate), then the SI account's party
        /// UUID is returned.
        /// </summary>
        [HttpPost("validate-credentials")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> ValidateCredentials([FromBody] SiUserCredentials credentials, CancellationToken cancellationToken)
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

            if (result.UserProfile?.Party?.PartyUuid is not { } fromPartyUuid)
            {
                return Unauthorized(new { Message = "Invalid credentials." });
            }

            Guid toPartyUuid = AuthenticationHelper.GetPartyUuId(HttpContext);
            if (toPartyUuid == Guid.Empty)
            {
                return BadRequest(new { Message = "Authenticated user has no party UUID." });
            }

            return await CreateConnectionAsync(fromPartyUuid, toPartyUuid, cancellationToken);
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
        /// token, enforces that the authenticated caller is the same person who requested the link
        /// (<c>to_party_uuid</c>), and then creates the connection in access-management **directly**
        /// (from the SI user to the authenticated person) so the redemption + delegation is a single
        /// atomic call and does not depend on the BFF performing the second step. On success returns
        /// the SI account's party UUID (<c>from_party_uuid</c>).
        /// </summary>
        [HttpPost("redeem-link")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> RedeemLink([FromBody] SelfIdentifiedLinkTokenRequest request, CancellationToken cancellationToken)
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
            // Create the connection directly in access-management (from SI user -> authenticated person),
            // rather than returning the party UUID and letting the BFF do it.
            return await CreateConnectionAsync(result.FromPartyUuid, result.ToPartyUuid, cancellationToken);
        }

        /// <summary>
        /// Creates the self-identified user connection (<paramref name="fromPartyUuid"/> -&gt;
        /// <paramref name="toPartyUuid"/>) in access-management and maps the outcome to an action result:
        /// <c>200 Ok(fromPartyUuid)</c> on success, <c>502 Bad Gateway</c> when access-management rejects
        /// the call. Shared by the credential and link-redemption flows so both delegate identically.
        /// </summary>
        private async Task<ActionResult> CreateConnectionAsync(Guid fromPartyUuid, Guid toPartyUuid, CancellationToken cancellationToken)
        {
            bool created = await _accessManagementClient.CreateSelfIdentifiedUserConnection(fromPartyUuid, toPartyUuid, cancellationToken);

            if (!created)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { Message = "Failed to create the self-identified user connection." });
            }

            return Ok(fromPartyUuid);
        }
    }
}
