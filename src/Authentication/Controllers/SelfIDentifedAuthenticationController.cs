using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for self identified authentication. This is used for users that are not registered in the system, but still need to be able to authenticate and use the system. The controller should have methods that validates the credentials of the user and returns a token that can be used for subsequent requests.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SelfIDentifedAuthenticationController(IUserProfileService profileService) : ControllerBase
    {
        private readonly IUserProfileService _profileService = profileService;

        /// <summary>
        /// Methods that validates username and password for a self identified user. This is used for users that are not registered in the system, but still need to be able to authenticate and use the system. The method should return a token that can be used for subsequent requests.
        /// </summary>
        [HttpPost("validate-credentials")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_PORTAL)]
        public async Task<ActionResult> ValidateCredentials(string userName, string password)
        {
            // Implement your logic to validate the credentials here
            // For example, you can check against a database or an external service
            UserCredentialVerificationResult isValid = await _profileService.ValidateCredentialsAsync(userName, password);
            if (isValid.UserProfile != null)
            {
                // Return a success response, e.g., a token or user information
                return Ok(isValid.UserProfile.Party.PartyUuid);
            }
            else
            {
                // Return an unauthorized response
                return Unauthorized(new { Message = "Invalid credentials." });
            }
        }

    }
}
