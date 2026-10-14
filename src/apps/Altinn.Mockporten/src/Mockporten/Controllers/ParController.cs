using Mockporten.Configuration;
using Mockporten.Models;
using Mockporten.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Mockporten.Controllers
{
    /// <summary>
    /// Pushed Authorization Requests (RFC 9126). Stateless: the pushed request
    /// is serialized into a signed, short-lived JWT and returned as the
    /// request_uri value - no server-side storage. See issue #1983 / #1409.
    /// </summary>
    [Route("par")]
    [ApiController]
    public class ParController : ControllerBase
    {
        private readonly IToken _tokenService;
        private readonly GeneralSettings _generalSettings;

        public ParController(IToken tokenService, IOptions<GeneralSettings> generalSettings)
        {
            _tokenService = tokenService;
            _generalSettings = generalSettings.Value;
        }

        [Consumes("application/x-www-form-urlencoded")]
        [HttpPost]
        public async Task<IActionResult> Index(
            [FromForm] string response_type,
            [FromForm] string client_id,
            [FromForm] string redirect_uri,
            [FromForm] string scope,
            [FromForm] string state,
            [FromForm] string nonce,
            [FromForm] string acr_values,
            [FromForm] string response_mode,
            [FromForm] string ui_locales,
            [FromForm] string prompt,
            [FromForm] string code_challenge,
            [FromForm] string code_challenge_method,
            [FromForm] string login_hint,
            [FromForm] string claims)
        {
            if (!_generalSettings.TestIdpEnabled)
            {
                return NotFound();
            }

            OidcAuthorizationModel model = new()
            {
                Response_type = response_type,
                Client_id = client_id,
                Redirect_uri = redirect_uri,
                Scope = scope,
                State = state,
                Nonce = nonce,
                Acr_values = acr_values,
                Response_mode = response_mode,
                Ui_locales = ui_locales,
                Prompt = prompt,
                Code_challenge = code_challenge,
                Code_challenge_method = code_challenge_method,
                Login_hint = login_hint,
                Claims = claims
            };

            string requestObject = await _tokenService.CreateRequestObject(model);

            // RFC 9126: 201 Created with the opaque request_uri and its lifetime.
            return StatusCode(201, new
            {
                request_uri = "urn:ietf:params:oauth:request_uri:" + requestObject,
                expires_in = 60
            });
        }
    }
}
