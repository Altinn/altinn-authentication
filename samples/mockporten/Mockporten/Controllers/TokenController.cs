using Mockporten.Configuration;
using Mockporten.Models;
using Mockporten.Services;
using Mockporten.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Mockporten.Controllers
{
    [Route("token")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IToken _tokenService;
        private readonly GeneralSettings _generalSettings;

        public TokenController(IToken tokenService, IOptions<GeneralSettings> generalSettings)
        {
            _tokenService = tokenService;
            _generalSettings = generalSettings.Value;
        }

        [Consumes("application/x-www-form-urlencoded")]
        [HttpPost]
        public async Task<ActionResult> Index(
            [FromForm] string client_id, 
            [FromForm] string grant_type, 
            [FromForm] string code, 
            [FromForm] string redirect_uri,
            [FromForm] string code_verifier,
            [FromForm] string client_assertion_type,
            [FromForm] string client_assertion,
            [FromForm] string assertion,
            [FromForm] string refresh_token)
        {
            if (!_generalSettings.TestIdpEnabled)
            {
                return NotFound();
            }

            string token;
            try
            {
                token = await _tokenService.GetTokenFromCode(code, code_verifier);
            }
            catch (OidcRequestException ex)
            {
                return BadRequest(new { error = ex.Error, error_description = ex.Message });
            }

            GrantResponse grantResponse = new GrantResponse
            {
                id_token = token,
                access_token = token,
                token_type = "Bearer",
                expires_in = 3600,
                refresh_token = "ADFSFDSFSDFDSFDSF"
            };
            return Ok(grantResponse);
        }
    }
}
