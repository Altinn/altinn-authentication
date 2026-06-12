using Mockporten.Configuration;
using Mockporten.Helpers;
using Mockporten.Models;
using Mockporten.Services;
using Mockporten.Services.Interface;
using Mockporten.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Mockporten.Controllers
{
    public class AuthorizeController : Controller
    {
        private readonly IToken _tokenService;
        private readonly ISharedAccessPasswordValidator _passwordValidator;
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger<AuthorizeController> _logger;

        public AuthorizeController(
            IToken tokenService,
            ISharedAccessPasswordValidator passwordValidator,
            IOptions<GeneralSettings> generalSettings,
            ILogger<AuthorizeController> logger)
        {
            _tokenService = tokenService;
            _passwordValidator = passwordValidator;
            _generalSettings = generalSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// OIDC login endpoint. Shows the login form. The previous
        /// <c>?pid=</c> shortcut that issued a code with no interaction has been
        /// removed - it was an unauthenticated pid-to-token oracle (#1983).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string response_type,
            [FromQuery] string client_id,
            [FromQuery] string redirect_uri,
            [FromQuery] string scope,
            [FromQuery] string state,
            [FromQuery] string nonce,
            [FromQuery] string acr_values,
            [FromQuery] string response_mode,
            [FromQuery] string ui_locales,
            [FromQuery] string prompt,
            [FromQuery] string code_challenge,
            [FromQuery] string code_challenge_method,
            [FromQuery] string login_hint,
            [FromQuery] string claims,
            [FromQuery] string request_uri)
        {
            if (!_generalSettings.TestIdpEnabled)
            {
                return TestIdpDisabled();
            }

            OidcAuthorizationModel viewModel;

            // PAR (RFC 9126): when a request_uri is supplied, the authorization
            // request parameters come from the (signed, validated) request
            // object only - loose query parameters are ignored.
            if (!string.IsNullOrEmpty(request_uri))
            {
                try
                {
                    viewModel = await _tokenService.ReadRequestObject(request_uri);
                }
                catch (OidcRequestException ex)
                {
                    _logger.LogWarning("Test-IDP rejected request_uri: {Error}", ex.Error);
                    return BadRequest(new { error = ex.Error, error_description = ex.Message });
                }
            }
            else
            {
                viewModel = new OidcAuthorizationModel
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
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Index(OidcAuthorizationModel viewModel)
        {
            if (!_generalSettings.TestIdpEnabled)
            {
                return TestIdpDisabled();
            }

            // Step 1: the single shared access password. This is the right to
            // use the Test-IDP at all (not a per-user credential). Until it is
            // proven we reveal nothing about the request - no redirect, no
            // distinction from a bad pid - so unauthenticated probers cannot
            // use this endpoint as an oracle.
            SharedPasswordResult passwordResult = _passwordValidator.Validate(viewModel.Password);
            if (passwordResult == SharedPasswordResult.LockedOut)
            {
                _logger.LogWarning("Test-IDP login locked out (shared password failures)");
                return StatusCode(429);
            }

            if (passwordResult != SharedPasswordResult.Success)
            {
                _logger.LogWarning(
                    "Test-IDP rejected invalid shared password for client_id {ClientId}",
                    viewModel.Client_id);
                return Unauthorized();
            }

            // Step 2: fail-closed Tenor gate. Only a well-formed synthetic
            // (Tenor) fødselsnummer may be minted. An ordinary fnr or a real
            // D-number can never pass. Defence-in-depth - the authoritative
            // check is RequireSyntheticPid in altinn-authentication (#1409).
            if (!NorwegianIdentityNumber.IsSyntheticTenorPid(viewModel.Pid))
            {
                _logger.LogWarning(
                    "Test-IDP rejected non-synthetic pid for client_id {ClientId}",
                    viewModel.Client_id);
                return AccessDenied(viewModel.Redirect_uri, viewModel.State);
            }

            string code = await _tokenService.GetAuthorizationCode(viewModel);

            UriBuilder baseUri = new(viewModel.Redirect_uri);
            if (baseUri.Query != null && baseUri.Query.Length > 1)
            {
                baseUri.Query = baseUri.Query + "&" + "code=" + code;
            }
            else
            {
                baseUri.Query = "code=" + code;
            }

            baseUri.Query = baseUri.Query + "&state=" + viewModel.State;

            return Redirect(baseUri.ToString());
        }

        private IActionResult TestIdpDisabled()
        {
            _logger.LogWarning("Test-IDP request blocked: TestIdpEnabled is false");
            return NotFound();
        }

        private IActionResult AccessDenied(string redirectUri, string state)
        {
            // OAuth error redirect back to the client when we have a usable
            // redirect_uri; otherwise a plain 403 so we never leak a token.
            if (!string.IsNullOrEmpty(redirectUri) && Uri.IsWellFormedUriString(redirectUri, UriKind.Absolute))
            {
                UriBuilder errorUri = new(redirectUri);
                string query = "error=access_denied&error_description=pid_not_synthetic";
                if (!string.IsNullOrEmpty(state))
                {
                    query += "&state=" + state;
                }

                errorUri.Query = errorUri.Query != null && errorUri.Query.Length > 1
                    ? errorUri.Query + "&" + query
                    : query;
                return Redirect(errorUri.ToString());
            }

            return StatusCode(403);
        }
    }
}
