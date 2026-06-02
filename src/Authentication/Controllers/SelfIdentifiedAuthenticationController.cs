using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Profile;
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
    public class SelfIdentifiedAuthenticationController(IUserProfileService profileService) : ControllerBase
    {
        private readonly IUserProfileService _profileService = profileService;

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
    }
}
