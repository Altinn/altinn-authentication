using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;

namespace Altinn.Platform.Authentication.Core.Services.Validation
{
    /// <summary>
    /// Stateless validator for OIDC /authorize basic semantics: response_type, scope, PKCE, prompt, max_age, nonce, acr_values, ui_locales, redirect_uri absolute.
    /// </summary>
    public sealed class AuthorizeRequestValidator : IAuthorizeRequestValidator
    {
        private static readonly HashSet<string> AllowedAcrValues = new(
            new[] { "selfregistered-email", "idporten-loa-substantial", "idporten-loa-high", "level0" },
            StringComparer.Ordinal);

        private static readonly HashSet<string> AllowedUiLocales = new(
            new[] { "nb", "nn", "en" }, StringComparer.Ordinal);

        public AuthorizeValidationError? ValidateBasics(AuthorizeRequest request)
        {
            if (request is null)
            {
                return Err("invalid_request", "request cannot be null.");
            }

            // response_type == "code" (case-sensitive per spec)
            if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
            {
                return Err("unsupported_response_type", "response_type must be 'code'.");
            }

            // scope contains openid
            if (request.Scopes is null || !request.Scopes.Contains("openid", StringComparer.Ordinal))
            {
                return Err("invalid_scope", "scope must include 'openid'.");
            }

            // redirect_uri must be absolute (we’ll do exact-match vs client later)
            if (request.RedirectUri is null || !request.RedirectUri.IsAbsoluteUri)
            {
                return Err("invalid_request", "redirect_uri must be an absolute URI.");
            }

            // PKCE: code_challenge present & S256
            if (string.IsNullOrWhiteSpace(request.CodeChallenge))
            {
                return Err("invalid_request", "code_challenge is required (PKCE).");
            }

            if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
            {
                return Err("invalid_request", "code_challenge_method must be 'S256'.");
            }

            // PKCE: base64url & length (43–128)
            if (request.CodeChallenge.Length is < 43 or > 128 || !IsBase64Url(request.CodeChallenge))
            {
                return Err("invalid_request", "code_challenge must be base64url, length 43–128.");
            }

            // prompt: none cannot combine with login/consent
            if (request.Prompts is not null &&
                request.Prompts.Contains("none", StringComparer.Ordinal) &&
                (request.Prompts.Contains("login", StringComparer.Ordinal) || request.Prompts.Contains("consent", StringComparer.Ordinal)))
            {
                return Err("invalid_request", "prompt=none cannot be combined with login or consent.");
            }

            // state recommended -> require (CSRF)
            if (string.IsNullOrWhiteSpace(request.State))
            {
                return Err("invalid_request", "state is required to protect against CSRF.");
            }

            // max_age >= 0
            if (request.MaxAge is < 0)
            {
                return Err("invalid_request", "max_age must be a non-negative integer.");
            }

            // acr_values allow-list (optional param)
            if (request.AcrValues is not null)
            {
                foreach (var v in request.AcrValues)
                {
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    if (!AllowedAcrValues.Contains(v))
                    {
                        return Err("invalid_request", $"acr_values contains unsupported value: '{v}'.");
                    }
                }
            }

            // ui_locales allow-list (optional param)
            if (request.UiLocales is not null)
            {
                foreach (var l in request.UiLocales)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    var lc = l.Trim().ToLowerInvariant();
                    if (!AllowedUiLocales.Contains(lc))
                        return Err("invalid_request", $"ui_locales contains unsupported value: '{l}'. Allowed: nb, nn, en.");
                }
            }

            // nonce required (your policy)
            if (string.IsNullOrWhiteSpace(request.Nonce))
            {
                return Err("invalid_request", "nonce is required.");
            }

            return null; // OK
        }

        private static bool IsBase64Url(string s)
        {
            try
            {
                _ = Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + 3 & ~3, '='));
                return true;
            }
            catch { return false; }
        }

        private static AuthorizeValidationError Err(string code, string desc)
            => new() { Error = code, Description = desc };
    }
}
