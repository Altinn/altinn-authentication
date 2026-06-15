#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthProblem = Altinn.Authentication.Core.Problems.Problem;

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
        /// Links a self-identified user account to the currently authenticated person. Validates the
        /// supplied SI credentials and, when valid, creates the connection from the SI user to the
        /// authenticated person directly in access-management. Returns the SI account's party UUID.
        /// </summary>
        [HttpPost("link")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> LinkAccount([FromBody] SiUserCredentials credentials, CancellationToken cancellationToken)
        {
            // Check the caller precondition before verifying credentials: a caller without a party UUID
            // can never complete the link, so rejecting first avoids recording failed-attempt/lockout
            // side effects on the target SI account (lockout-abuse vector).
            Guid toPartyUuid = AuthenticationHelper.GetPartyUuId(HttpContext);
            if (toPartyUuid == Guid.Empty)
            {
                return AuthProblem.SelfIdentifiedLink_MissingPartyUuid.ToActionResult();
            }

            UserCredentialVerificationResult result =
                await _profileService.ValidateCredentialsAsync(credentials.UserName, credentials.Password);

            if (result.IsLocked)
            {
                return AuthProblem.SelfIdentifiedLink_AccountLocked.ToActionResult();
            }

            if (result.WrongUserType)
            {
                return AuthProblem.SelfIdentifiedLink_WrongUserType.ToActionResult();
            }

            if (result.UserProfile?.Party?.PartyUuid is not { } fromPartyUuid)
            {
                return AuthProblem.SelfIdentifiedLink_InvalidCredentials.ToActionResult();
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
                return AuthProblem.SelfIdentifiedLink_MissingPartyUuid.ToActionResult();
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
                return AuthProblem.SelfIdentifiedLink_MissingPartyUuid.ToActionResult();
            }

            SelfIdentifiedLinkTokenResult result = await _linkTokenService.ValidateAsync(request.Token, cancellationToken);

            if (!result.IsValid)
            {
                return AuthProblem.SelfIdentifiedLink_InvalidToken.ToActionResult();
            }

            // Requester == consumer: the link may only be redeemed by the same person who requested it.
            if (result.ToPartyUuid != callerPartyUuid)
            {
                return AuthProblem.SelfIdentifiedLink_TokenNotForCaller.ToActionResult();
            }

            // Single-use (jti) enforcement is not yet implemented - tracked as an open item in #2035.
            // Create the connection directly in access-management (from SI user -> authenticated person),
            // rather than returning the party UUID and letting the BFF do it.
            return await CreateConnectionAsync(result.FromPartyUuid, result.ToPartyUuid, cancellationToken);
        }

        /// <summary>
        /// Creates the self-identified user connection (<paramref name="fromPartyUuid"/> -&gt;
        /// <paramref name="toPartyUuid"/>) in access-management and maps the outcome to an action result:
        /// <c>200 Ok(fromPartyUuid)</c> on success, or the
        /// <see cref="AuthProblem.SelfIdentifiedLink_ConnectionFailed"/> problem when access-management
        /// rejects the call. Shared by the credential and link-redemption flows so both delegate identically.
        /// </summary>
        private async Task<ActionResult> CreateConnectionAsync(Guid fromPartyUuid, Guid toPartyUuid, CancellationToken cancellationToken)
        {
            bool created = await _accessManagementClient.CreateSelfIdentifiedUserConnection(fromPartyUuid, toPartyUuid, cancellationToken);

            if (!created)
            {
                return AuthProblem.SelfIdentifiedLink_ConnectionFailed.ToActionResult();
            }

            return Ok(fromPartyUuid);
        }
    }
}
