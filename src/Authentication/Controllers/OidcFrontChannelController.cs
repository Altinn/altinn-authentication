using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for handling OIDC front-channel authentication endpoints.
    /// </summary>
    [Route("authentication/api/v1")]
    public class OidcFrontChannelController : Controller
    {
        /// <summary>
        /// Initiates the OIDC authorization flow.
        /// </summary>
        /// <param name="q">The authorization request parameters.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the authorization request.</returns>
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize([FromQuery] AuthorizeRequestDto q)
        {

            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Handles the callback from the upstream OIDC provider.
        /// </summary>
        /// <param name="code">The authorization code returned by the provider.</param>
        /// <param name="state">The state parameter to correlate the request.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the callback.</returns>
        [HttpGet("upstream/callback")]
        public async Task<IActionResult> UpstreamCallback([FromQuery] string code, [FromQuery] string state)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Handles the OIDC front-channel logout request.
        /// </summary>
        /// <param name="sid">The session identifier for logout.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the logout request.</returns>
        [HttpGet("logout/frontchannel")]
        public IActionResult Logout([FromQuery] string sid) 
        {
            throw new System.NotImplementedException();
        }
    }
}
