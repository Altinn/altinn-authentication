using System;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for handling OIDC front-channel authentication endpoints.
    /// </summary>
    [Route("authentication/api/v1")]
    public class OidcFrontChannelController(IOidcServerService oidcServerService) : Controller
    {
        private IOidcServerService _oidcServerService = oidcServerService;

        /// <summary>
        /// Initiates the OIDC authorization flow.
        /// Will validate the request, persist a login transaction, and redirect to the upstream OIDC provider.
        /// </summary>
        /// <param name="q">The authorization request parameters.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the authorization request.</returns>
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize([FromQuery] AuthorizeRequestDto q)
        {
            AuthorizeRequest req;
            try
            {
                req = AuthorizeRequestMapper.Normalize(q);
            }
            catch (ArgumentException ex)
            {
                // If redirect_uri is valid → OIDC error redirect with state
                // Else → local HTML error
                return BadRequest(ex.Message);
            }
       
            AuthorizeResult result = await _oidcServerService.Authorize(req);

            foreach (var c in result.Cookies)
            {
                Response.Cookies.Append(c.Name, c.Value, new CookieOptions
                {
                    HttpOnly = c.HttpOnly,
                    Secure = c.Secure,
                    Path = c.Path ?? "/",
                    Domain = c.Domain,
                    Expires = c.Expires,
                    SameSite = c.SameSite
                });
            }

            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Pragma"] = "no-cache";

            return result.Kind switch
            {
                AuthorizeResultKind.RedirectUpstream
                    => Redirect(result.UpstreamAuthorizeUrl!.ToString()),

                AuthorizeResultKind.ErrorRedirectToClient
                    => Redirect(BuildOidcErrorRedirect(result.ClientRedirectUri!, result.Error!, result.ErrorDescription, result.ClientState)),

                AuthorizeResultKind.LocalError
                    => StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage), // or return View("Error", ...)

                AuthorizeResultKind.RenderInteraction
                    => View(result.ViewName!, result.ViewModel),

                _ => StatusCode(500)
            };
        }

        private string BuildOidcErrorRedirect(Uri uri, string v, string errorDescription, string clientState)
        {
            throw new NotImplementedException();
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
