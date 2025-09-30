using System;
using System.Net;
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
    /// OIDC token endpoint controller.
    /// </summary>
    [ApiController]
    [Route("authentication/api/v1")]
    public sealed class OidcTokenController(ITokenService tokenService) : ControllerBase
    {
        private readonly ITokenService _tokenService = tokenService;

        /// <summary>
        /// Creates a new access token (and optionally an ID token) in exchange for a valid authorization code.
        /// </summary>
        [HttpPost("token")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Token([FromForm] TokenRequestForm form, CancellationToken ct)
        {
            // Cache directives per spec
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            // Content-type is handled by [Consumes]. Still validate grant_type quickly:
            if (!string.Equals(form.GrantType, "authorization_code", StringComparison.Ordinal))
            {
                return OAuthError("unsupported_grant_type", "Only authorization_code is supported.");
            }

            // Build a normalized request for the service
            var req = new TokenRequest
            {
                GrantType = form.GrantType!,
                Code = form.Code ?? string.Empty,
                RedirectUri = form.RedirectUri is null ? null : new Uri(form.RedirectUri, UriKind.Absolute),
                ClientId = form.ClientId,              // may be null if auth header is used
                CodeVerifier = form.CodeVerifier,
                
                // Client authentication (header or form)
                ClientAuth = ParseClientAuth(Request, form)
            };

            TokenResult result = await _tokenService.ExchangeAuthorizationCodeAsync(req, ct);

            return result.Kind switch
            {
                TokenResultKind.Success => Ok(new TokenResponseDto
                {
                    access_token = result.AccessToken!,
                    id_token = result.IdToken,
                    token_type = "Bearer",
                    expires_in = result.ExpiresIn,
                    scope = result.Scope
                }),

                TokenResultKind.InvalidClient => Unauthorized(OAuthErrorBody(result.Error!, result.ErrorDescription)),
                TokenResultKind.InvalidRequest or TokenResultKind.InvalidGrant
                    => BadRequest(OAuthErrorBody(result.Error!, result.ErrorDescription)),

                TokenResultKind.ServerError => StatusCode((int)HttpStatusCode.InternalServerError, OAuthErrorBody("server_error", result.ErrorDescription)),
                _ => StatusCode((int)HttpStatusCode.InternalServerError, OAuthErrorBody("server_error", "Unexpected"))
            };
        }

        private static TokenClientAuth ParseClientAuth(HttpRequest request, TokenRequestForm form)
        {
            // client_secret_basic takes precedence if header present
            if (request.Headers.TryGetValue("Authorization", out var auth) && auth.Count > 0)
            {
                var v = auth.ToString();
                if (v.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    var b64 = v.Substring("Basic ".Length).Trim();
                    string decoded;
                    try 
                    { 
                        decoded = Encoding.UTF8.GetString(Convert.FromBase64String(b64)); 
                    }
                    catch 
                    { 
                        decoded = string.Empty; 
                    }

                    var idx = decoded.IndexOf(':');
                    if (idx > 0)
                    {
                        return TokenClientAuth.ClientSecretBasic(
                            clientId: decoded[..idx],
                            secret: decoded[(idx + 1)..]);
                    }
                }
            }

            // Fallback to client_secret_post if values present
            if (!string.IsNullOrEmpty(form.ClientId) && !string.IsNullOrEmpty(form.ClientSecret))
            {
                return TokenClientAuth.ClientSecretPost(form.ClientId!, form.ClientSecret!);
            }

            // Private key JWT (future; wire when you add it)
            if (!string.IsNullOrEmpty(form.ClientAssertionType) &&
                !string.IsNullOrEmpty(form.ClientAssertion))
            {
                return TokenClientAuth.PrivateKeyJwt(form.ClientId!, form.ClientAssertionType!, form.ClientAssertion!);
            }

            // Public clients (PKCE only)
            if (!string.IsNullOrEmpty(form.ClientId))
            {
                return TokenClientAuth.None(form.ClientId!);
            }

            return TokenClientAuth.Missing();
        }

        private static IActionResult OAuthError(string code, string? desc)
            => new BadRequestObjectResult(OAuthErrorBody(code, desc));

        private static object OAuthErrorBody(string code, string? desc) => new
        {
            error = code,
            error_description = desc
        };
    }
}
