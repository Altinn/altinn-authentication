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
        /// Creates access/ID tokens via authorization code exchange,
        /// or rotates/returns tokens via refresh_token grant.
        /// </summary>
        [HttpPost("token")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Token([FromForm] TokenRequestForm form, CancellationToken cancellationToken)
        {
            // RFC 6749 §5.1: prevent caching
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            if (string.IsNullOrWhiteSpace(form.GrantType))
            {
                return OAuthError("invalid_request", "Missing grant_type.");
            }

            // Common: parse client auth from header or form (kept as your helper)
            TokenClientAuth clientAuth = ParseClientAuth(Request, form);

            switch (form.GrantType)
            {
                case "authorization_code":
                    {
                        // Basic shape validation here; deep validation in service
                        if (string.IsNullOrWhiteSpace(form.Code))
                        {
                            return OAuthError("invalid_request", "Missing code.");
                        }

                        if (string.IsNullOrWhiteSpace(form.RedirectUri))
                        {
                            return OAuthError("invalid_request", "Missing redirect_uri.");
                        }

                        TokenRequest req = new()
                        {
                            GrantType = "authorization_code",
                            Code = form.Code!,
                            RedirectUri = Uri.TryCreate(form.RedirectUri, UriKind.Absolute, out var ru) ? ru : null,
                            CodeVerifier = form.CodeVerifier,
                            ClientId = form.ClientId,      // may be null when using Basic
                            ClientAuth = clientAuth
                        };

                        TokenResult result = await _tokenService.ExchangeAuthorizationCodeAsync(req, cancellationToken);
                        return ToHttpResult(result);
                    }

                case "refresh_token":
                    {
                        if (string.IsNullOrWhiteSpace(form.RefreshToken))
                        {
                            return OAuthError("invalid_request", "Missing refresh_token.");
                        }

                        RefreshTokenRequest req = new()
                        {
                            GrantType = "refresh_token",
                            RefreshToken = form.RefreshToken!,
                            Scope = form.Scope,            // optional down-scope request (subset only)
                            ClientId = form.ClientId,      // may be null when using Basic
                            ClientAuth = clientAuth
                        };

                        TokenResult result = await _tokenService.RefreshAsync(req, cancellationToken);
                        return ToHttpResult(result);
                    }

                default:
                    return OAuthError("unsupported_grant_type", "Supported grants: authorization_code, refresh_token.");
            }
        }

        // --- helpers ---

        private IActionResult ToHttpResult(TokenResult result)
        {
            return result.Kind switch
            {
                TokenResultKind.Success => Ok(new TokenResponseDto
                {
                    access_token = result.AccessToken!,
                    id_token = result.IdToken,
                    token_type = "Bearer",
                    expires_in = result.ExpiresIn,
                    scope = result.Scope,
                    refresh_token = result.RefreshToken,
                    refresh_token_expires_in = result.RefreshTokenExpiresIn
                }),

                TokenResultKind.InvalidClient
                    => Unauthorized(OAuthErrorBody(result.Error!, result.ErrorDescription)),
                TokenResultKind.InvalidRequest or TokenResultKind.InvalidGrant
                    => BadRequest(OAuthErrorBody(result.Error!, result.ErrorDescription)),

                TokenResultKind.ServerError
                    => StatusCode((int)HttpStatusCode.InternalServerError, OAuthErrorBody("server_error", result.ErrorDescription)),

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
                if (string.IsNullOrEmpty(form.ClientId))
                {
                    return TokenClientAuth.Missing();
                }

                return TokenClientAuth.PrivateKeyJwt(form.ClientId, form.ClientAssertionType!, form.ClientAssertion!);
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
