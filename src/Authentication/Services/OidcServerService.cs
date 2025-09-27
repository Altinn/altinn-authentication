using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Service that implements the OIDC <c>/authorize</c> front-channel flow for Altinn Authentication as an OP.
    /// </summary>
    public class OidcServerService(IOidcServerClientRepository oidcServerClientRepository) : IOidcServerService
    {
        private IOidcServerClientRepository _oidcServerClientRepository = oidcServerClientRepository;

        /// <summary>
        /// Handles an incoming OIDC <c>/authorize</c> request and returns a high-level outcome that the controller converts to HTTP.
        /// </summary>
        public async Task<AuthorizeResult> Authorize(AuthorizeRequest request)
        {
            // Local helper to choose error redirect or local error based on redirect_uri validity
            AuthorizeResult Fail(string error, string description)
            {
                // If redirect_uri is an absolute URI, prefer an error redirect
                if (TryAbsoluteUri(request.RedirectUri.AbsoluteUri, out var ru))
                {
                    return AuthorizeResult.ErrorRedirect(ru!, error, description, request.State);
                }

                // Otherwise, we cannot safely redirect → local error
                return AuthorizeResult.LocalError(400, error, description);
            }

            // ========= 0) Guard =========
            ArgumentNullException.ThrowIfNull(request);

            // ========= 1) Validate basic OIDC semantics =========

            // response_type == "code" (spec is case-sensitive)
            if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
            {
                return Fail("unsupported_response_type", "response_type must be 'code'.");
            }

            if (!request.Scopes.Contains("openid", StringComparer.Ordinal))
            {
                return Fail("invalid_scope", "scope must include 'openid'.");
            }

            if (string.IsNullOrWhiteSpace(request.CodeChallenge))
            {
                return Fail("invalid_request", "code_challenge is required (PKCE).");
            }

            if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
            {
                return Fail("invalid_request", "code_challenge_method must be 'S256'.");
            }

            // PKCE: verifier-derived challenge constraints (RFC 7636) – length & charset for challenge
            // challenge is base64url-encoded SHA256; length typically 43-128 and charset base64url
            if (request.CodeChallenge.Length is < 43 or > 128 || !IsBase64Url(request.CodeChallenge))
            {
                return Fail("invalid_request", "code_challenge must be base64url, length 43–128.");
            }

            // prompt validation: 'none' cannot be combined with 'login' or 'consent'
            if (request.Prompts.Contains("none", StringComparer.Ordinal) &&
                (request.Prompts.Contains("login", StringComparer.Ordinal) || request.Prompts.Contains("consent", StringComparer.Ordinal)))
            {
                return Fail("invalid_request", "prompt=none cannot be combined with login or consent.");
            }

            // max_age >= 0 (if present)
            if (request.MaxAge is < 0)
            {
                return Fail("invalid_request", "max_age must be a non-negative integer.");
            }

            (bool acrOk, string badAcr) = AreAcrValuesSupported(request.AcrValues);
            if (!acrOk)
            {
                return Fail("invalid_request", $"acr_values contains unsupported value: '{badAcr}'. Allowed: {string.Join(", ", AllowedAcrValues)}");
            }

            var (uiOk, badUi) = AreUiLocalesSupported(request.UiLocales);
            if (!uiOk)
            {
                return Fail("invalid_request", $"ui_locales contains unsupported value: '{badUi}'. Allowed: nb, nn, en");
            }

            // nonce recommended (optionally enforce)
            if (string.IsNullOrWhiteSpace(request.Nonce))
            {
                return Fail("invalid_request", "nonce is required.");
            }

            // If we reach here, basic validation passed.
            // ... continue with client lookup, redirect_uri exact-match, PKCE storage, etc.
            // (Return a placeholder for now so code compiles; replace with your actual flow.)

            OidcClient client = await _oidcServerClientRepository.GetClientAsync(request.ClientId);

            return AuthorizeResult.LocalError(501, "not_implemented", "Next steps: client lookup & upstream routing.");
        }

        // ========= 2) Client lookup & policy =========
        // TODO: load client by request.ClientId
        // TODO: ensure request.RedirectUri exactly matches one of client.redirect_uris
        // TODO: ensure requested scopes are subset of client.allowed_scopes

        // ========= 3) Handle PAR / JAR if present =========
        // TODO: if request.RequestUri != null -> load par_request, verify TTL and client_id match, override parameters
        // TODO: if request.RequestObject != null -> validate JWS/JWE, extract claims/params (optional phase)

        // ========= 4) Existing IdP session reuse (optional optimization) =========
        // TODO: try locate valid oidc_session for (client_id, subject) meeting acr/max_age
        // TODO: if reusable and no prompt=login: proceed to issue downstream authorization_code (future extension)

        // ========= 5) Persist login_transaction (downstream) =========
        // TODO: insert login_transaction with:
        //  - client_id, redirect_uri, scopes[], state, nonce, acr_values, prompt, ui_locales, max_age
        //  - code_challenge, code_challenge_method='S256'
        //  - created_at, expires_at = now + 10 min
        //// Capture generated request_id for correlation.
        //Guid requestId = Guid.NewGuid(); // replace with DB-generated id

        //    // ========= 6) Choose upstream and derive upstream params =========
        //    // TODO: select upstream provider by acr_values/scope/policy (ID-porten default)
        //    // TODO: generate upstream_state, upstream_nonce, upstream_code_verifier (43-128 chars), upstream_code_challenge=S256
        //    string upstreamState = /* generate cryptographic random */ Guid.NewGuid().ToString("N");
        //    var upstreamAuthorizeUrl = new Uri("https://login.idporten.no/authorize?..." /* build full URL with params */);

        //    // ========= 7) Persist login_transaction_upstream =========
        //    // TODO: insert row bound to requestId with upstream_state, nonce, redirect_uri (our callback), verifier/challenge, etc.

        //    // ========= 8) Cookies to set (optional) =========
        //    var cookies = new List<CookieInstruction>
        //    {
        //        // Example: a correlation cookie or request marker (no PII)
        //        // new CookieInstruction { Name = "altinn_auth_corr", Value = correlationId, HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMinutes(10) }
        //    };

        //    // NOTE: On validation failure:
        //    // return AuthorizeResult.ErrorRedirect(validClientRedirectUri, "invalid_request", "reason", request.State);
        //    // or if redirect is unsafe:
        //    // return AuthorizeResult.LocalError(400, "invalid_request", "Bad authorize request");

        //    // ========= 9) Return redirect upstream =========
        //    return AuthorizeResult.RedirectUpstream(
        //        upstreamAuthorizeUrl,
        //        upstreamState,
        //        requestId,
        //        cookies);
        //}

        static bool TryAbsoluteUri(string? s, out Uri? uri)
        {
            if (!string.IsNullOrWhiteSpace(s) && Uri.TryCreate(s, UriKind.Absolute, out var u))
            {
                uri = u;
                return true;
            }

            uri = null;
            return false;
        }

        static bool IsBase64Url(string s)
        {
            // Allowed: A–Z, a–z, 0–9, '-', '_'; no padding '='
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= 'A' && c <= 'Z')
                       || (c >= 'a' && c <= 'z')
                       || (c >= '0' && c <= '9')
                       || c == '-' || c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly HashSet<string> AllowedAcrValues = new(
            new[] { "selfregistered-email", "idporten-loa-substantial", "idporten-loa-high" },
            StringComparer.Ordinal // case-sensitive; change to OrdinalIgnoreCase if you prefer
        );

        private static (bool ok, string? offending) AreAcrValuesSupported(string[] acrValues)
        {
            if (acrValues is null || acrValues.Length == 0)
            {
                return (true, null);
            }

            foreach (var v in acrValues)
            {
                if (string.IsNullOrWhiteSpace(v))
                {
                    continue; // treat empty tokens as ignored
                }

                if (!AllowedAcrValues.Contains(v))
                {
                    return (false, v);
                }
            }

            return (true, null);
        }

        // Allowed UI locales (lowercase, exact)
        private static readonly HashSet<string> AllowedUiLocales = new(
            new[] { "nb", "nn", "en" },
            StringComparer.Ordinal
        );

        private static (bool ok, string? offending) AreUiLocalesSupported(string[]? locales)
        {
            if (locales is null || locales.Length == 0)
            {
                return (true, null);
            }

            foreach (var l in locales)
            {
                if (string.IsNullOrWhiteSpace(l))
                {
                    continue; // ignore empties from weird clients
                }

                var lc = l.Trim().ToLowerInvariant();
                if (!AllowedUiLocales.Contains(lc))
                {
                    return (false, l);
                }
            }

            return (true, null);
        }
    }
}
