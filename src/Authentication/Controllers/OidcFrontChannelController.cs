#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
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
        private readonly IOidcServerService _oidcServerService = oidcServerService;

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
            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
            string ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrEmpty(ua) ? null : ComputeSha256Base64Url(ua);
            Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();

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

            // in OidcFrontChannelController
            AuthorizeResult result = await _oidcServerService.Authorize(req, cancellationToken);

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

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

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

        /// <summary>
        /// Handles the callback from the upstream OIDC provider.
        /// </summary>
        [HttpGet("upstream/callback")]
        public async Task<IActionResult> UpstreamCallback([FromQuery] UpstreamCallbackDto q, CancellationToken ct = default)
        {
            // Gather diagnostics
            var ip = HttpContext.Connection.RemoteIpAddress;
            var ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrEmpty(ua) ? null : ComputeSha256Base64Url(ua);
            Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();

            var input = new UpstreamCallbackInput
            {
                Code = q.Code,
                State = q.State,
                Error = q.Error,
                ErrorDescription = q.ErrorDescription,
                Iss = q.Iss,
                ClientIp = ip!,
                UserAgentHash = userAgentHash,
                CorrelationId = corr
            };

            var result = await _oidcServerService.HandleUpstreamCallback(input, ct);

            // Set no-store for auth responses
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Pragma"] = "no-cache";

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

            return result.Kind switch
            {
                UpstreamCallbackResultKind.RedirectToClient =>
                    Redirect(BuildDownstreamSuccessRedirect(result.ClientRedirectUri!, result.DownstreamCode!, result.ClientState)),

                UpstreamCallbackResultKind.ErrorRedirectToClient =>
                    Redirect(BuildOidcErrorRedirect(result.ClientRedirectUri!, result.Error!, result.ErrorDescription, result.ClientState)),

                UpstreamCallbackResultKind.LocalError =>
                    StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage),

                _ => StatusCode(500)
            };
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

        private static string ComputeSha256Base64Url(string input)
        {
            ArgumentNullException.ThrowIfNull(input);

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return ComputeSha256Base64Url(bytes);
        }

        private static string ComputeSha256Base64Url(ReadOnlySpan<byte> data)
        {
            // SHA256.HashData is allocation-free and fast
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(data, hash);

            // Convert to Base64URL: replace '+' -> '-', '/' -> '_', and trim '='
            string b64 = Convert.ToBase64String(hash);
            return b64.Replace('+', '-')
                      .Replace('/', '_')
                      .TrimEnd('=');
        }

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
    }
}
