#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for handling OIDC front-channel authentication endpoints.
    /// </summary>
    [Route("authentication/api/v1")]
    public class OidcFrontChannelController(IOidcServerService oidcServerService, IOptions<GeneralSettings> generalSettings) : Controller
    {
        private readonly IOidcServerService _oidcServerService = oidcServerService;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;

        /// <summary>
        /// Initiates the OIDC authorization flow.
        /// Will validate the request, persist a login transaction, and redirect to the upstream OIDC provider.
        /// </summary>
        /// <param name="q">The authorization request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the authorization request.</returns>
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize([FromQuery] AuthorizeRequestDto q, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            string ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrEmpty(ua) ? null : Hashing.Sha256Base64Url(ua);
            Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();
            string? sessionHandle = Request.Cookies.TryGetValue(_generalSettings.AltinnSessionCookieName, out var sh) ? sh : null;
            string cookieName = Request.Cookies[_generalSettings.SblAuthCookieEnvSpecificName] != null ? _generalSettings.SblAuthCookieEnvSpecificName : _generalSettings.SblAuthCookieName;
            string? encryptedTicket = Request.Cookies[cookieName];

            AuthorizeRequest req;
            try
            {
                req = AuthorizeRequestMapper.Normalize(q, ip, userAgentHash, corr);
            }
            catch (ArgumentException ex)
            {
                // If redirect_uri is valid → OIDC error redirect with state
                // Else → local HTML error
                return BadRequest(ex.Message);
            }

            ClaimsPrincipal claimsPrincipal = HttpContext.User;

            // in OidcFrontChannelController
            AuthorizeResult result = await _oidcServerService.Authorize(req, claimsPrincipal, sessionHandle, encryptedTicket, cancellationToken);

            SetCookies(result.Cookies);

            SetCacheHeaders();

            return result.Kind switch
            {
                AuthorizeResultKind.RedirectUpstream
                    => Redirect(result.UpstreamAuthorizeUrl!.ToString()),
                AuthorizeResultKind.RedirectToDownstreamBasedOnReusedSession
                    => Redirect(BuildDownstreamSuccessRedirect(result.ClientRedirectUri!, result.DownstreamCode!, result.ClientState)),
                AuthorizeResultKind.ErrorRedirectToClient
                    => Redirect(BuildOidcErrorRedirect(result.ClientRedirectUri!, result.Error!, result.ErrorDescription, result.ClientState)),
                AuthorizeResultKind.LocalError
                    => StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage), // or return View("Error", ...)
                AuthorizeResultKind.RenderInteraction
                    => View(result.ViewName!, result.ViewModel),

                _ => StatusCode(500)
            };
        }

        /// <summary>
        /// Handles the callback from the upstream OIDC provider.
        /// </summary>
        [HttpGet("upstream/callback")]
        public async Task<IActionResult> UpstreamCallback([FromQuery] UpstreamCallbackDto q, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Gather diagnostics
            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            string ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrEmpty(ua) ? null : Hashing.Sha256Base64Url(ua);
            Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();

            UpstreamCallbackInput input = new()
            {
                Code = q.Code,
                State = q.State,
                Error = q.Error,
                ErrorDescription = q.ErrorDescription,
                Iss = q.Iss,
                ClientIp = ip ?? System.Net.IPAddress.Loopback,
                UserAgentHash = userAgentHash,
                CorrelationId = corr
            };

            string? sessionHandle = Request.Cookies.TryGetValue(_generalSettings.AltinnSessionCookieName, out var sh) ? sh : null;

            UpstreamCallbackResult result = await _oidcServerService.HandleUpstreamCallback(input, sessionHandle, cancellationToken);

            SetCacheHeaders();

            SetCookies(result.Cookies);

            return result.Kind switch
            {
                UpstreamCallbackResultKind.RedirectToClient =>
                    Redirect(BuildDownstreamSuccessRedirect(result.ClientRedirectUri!, result.DownstreamCode!, result.ClientState)),
                UpstreamCallbackResultKind.RedirectToGoTo =>
                    Redirect(result.ClientRedirectUri!.AbsoluteUri),
                UpstreamCallbackResultKind.ErrorRedirectToClient =>
                    Redirect(BuildOidcErrorRedirect(result.ClientRedirectUri!, result.Error!, result.ErrorDescription, result.ClientState)),

                UpstreamCallbackResultKind.LocalError =>
                    StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage),

                _ => StatusCode(500)
            };
        }

        /// <summary>
        /// Handles front-channel logout requests from upstream OIDC providers.
        /// Returns simple HTML response with no-store headers and best-effort cookie operations.
        /// </summary>
        [HttpGet("upstream/frontchannel-logout")]
        public async Task<IActionResult> UpstreamFrontChannelLogout(
            [FromQuery] string iss,
            [FromQuery] string sid,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(iss) || string.IsNullOrWhiteSpace(sid))
            {
                return BadRequest("Missing iss or sid.");
            }

            UpstreamFrontChannelLogoutInput logoutInput = new()
            {
                Issuer = iss,
                UpstreamSid = sid,
                User = HttpContext.User
            };

            UpstreamFrontChannelLogoutResult result = await _oidcServerService.HandleUpstreamFrontChannelLogoutAsync(logoutInput, cancellationToken);

            SetCacheHeaders();

            // Best-effort cookie ops (may be blocked by 3p cookie settings)
            SetCookies(result.Cookies);

            // Keep body tiny so IdP iframes finish fast
            return Content("OK");
        }

        /// <summary>
        /// OIDC end_session_endpoint – RP-initiated logout.
        /// Accepts id_token_hint, post_logout_redirect_uri, and state.
        /// Delegates to the service; controller only appends cookies and returns redirect/page.
        /// </summary>
        [HttpGet("openid/logout")]
        public async Task<IActionResult> EndSession(
            [FromQuery] string? id_token_hint,
            [FromQuery] string? post_logout_redirect_uri,
            [FromQuery] string? state,
            CancellationToken cancellationToken = default)
        {
            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            string ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrWhiteSpace(ua) ? null : Hashing.Sha256Base64Url(ua);

            EndSessionInput input = new EndSessionInput
            {
                IdTokenHint = id_token_hint,
                PostLogoutRedirectUri = TryParseAbsoluteUri(post_logout_redirect_uri),
                State = state,
                User = HttpContext.User,
                ClientIp = ip,
                UserAgentHash = userAgentHash
            };

            EndSessionResult result = await _oidcServerService.EndSessionAsync(input, cancellationToken);

            SetCacheHeaders();

            // Apply cookie instructions produced by the service
            SetCookies(result.Cookies);

            if (result.RedirectUri is not null)
            {
                return Redirect(result.RedirectUri.ToString());
            }

            return Content("You are logged out.");
        }

        private static Uri? TryParseAbsoluteUri(string? s) =>
            Uri.TryCreate(s, UriKind.Absolute, out var u) ? u : null;

        private static string BuildOidcErrorRedirect(Uri redirectUri, string error, string? errorDescription, string? clientState)
        {
            ArgumentNullException.ThrowIfNull(redirectUri);

            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("error is required", nameof(error));
            }

            // Sanitize/trim human text to avoid CRLF and overly long URLs
            static string Sanitize(string s) =>
                s.Replace("\r", " ").Replace("\n", " ").Trim();

            static string Truncate(string s, int max) =>
                s.Length <= max ? s : s.Substring(0, max);

            var ub = new UriBuilder(redirectUri);
            var q = System.Web.HttpUtility.ParseQueryString(ub.Query);

            q["error"] = error;

            if (!string.IsNullOrWhiteSpace(errorDescription))
            {
                q["error_description"] = Truncate(Sanitize(errorDescription), 512);
            }

            if (!string.IsNullOrWhiteSpace(clientState))
            {
                q["state"] = clientState;
            }

            ub.Query = q.ToString()!;  // HttpUtility will URL-encode as needed
            return ub.Uri.ToString();
        }

        private static string BuildDownstreamSuccessRedirect(Uri redirectUri, string code, string? state)
        {
            var ub = new UriBuilder(redirectUri);
            var q = System.Web.HttpUtility.ParseQueryString(ub.Query);
            q["code"] = code;
            if (!string.IsNullOrWhiteSpace(state))
            {
                q["state"] = state;
            }

            ub.Query = q.ToString()!;
            return ub.Uri.ToString();
        }

        private void SetCacheHeaders()
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
        }

        private void SetCookies(IReadOnlyList<CookieInstruction> Cookies)
        {
            foreach (CookieInstruction c in Cookies)
            {
                Response.Cookies.Append(c.Name, c.Value ?? string.Empty, new CookieOptions
                {
                    HttpOnly = c.HttpOnly,
                    Secure = c.Secure,
                    Path = c.Path ?? "/",
                    Domain = c.Domain,
                    Expires = c.Expires,
                    SameSite = c.SameSite
                });
            }
        }
    }
}
