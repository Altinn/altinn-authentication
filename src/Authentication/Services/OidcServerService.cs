using System;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Service that implements the OIDC <c>/authorize</c> front-channel flow for Altinn Authentication as an OP.
    /// <para>
    /// This service is called by the MVC controller for GET/POST <c>/authentication/api/v1/authorize</c>.
    /// It performs all protocol validation, policy/routing decisions, and state persistence needed to either:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Redirect the user-agent to an upstream IdP authorize endpoint (ID-porten, Feide, Test-IDP), or</description></item>
    ///   <item><description>Return an OIDC error redirect back to the client’s <c>redirect_uri</c>, or</description></item>
    ///   <item><description>Render an interaction step (e.g., consent or actor selection), or</description></item>
    ///   <item><description>Return a local error page when a safe redirect cannot be done.</description></item>
    /// </list>
    /// <para>
    /// The method’s outcome is expressed by <see cref="AuthorizeResult"/> which the controller translates to HTTP
    /// (302 redirect upstream / 302 error redirect to client / local error / view).
    /// </para>
    /// </summary>
    /// <remarks>
    /// <h2>Responsibilities</h2>
    /// <list type="number">
    ///   <item>
    ///     <description><b>Normalize &amp; validate request</b>: <c>response_type=code</c>, <c>scope</c> contains <c>openid</c>,
    ///     <c>client_id</c> present, <c>redirect_uri</c> absolute, <c>code_challenge_method=S256</c>, optional <c>nonce</c>,
    ///     and well-formed <c>acr_values</c>/<c>prompt</c>/<c>ui_locales</c>/<c>max_age</c>. If <c>request_uri</c> (PAR) is present,
    ///     resolve and override query parameters from the stored PAR object.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Client lookup &amp; policy</b>: resolve the registered client; ensure <c>redirect_uri</c> is an exact match;
    ///     ensure all requested scopes are ⊆ <c>allowed_scopes</c>; enforce client <c>token_endpoint_auth_method</c> and other client policy gates.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>PKCE</b>: require <c>code_challenge</c> and <c>S256</c>. Reject <c>plain</c>.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Existing IdP session</b>: if a valid Altinn IdP session exists and meets <c>max_age</c>/<c>acr</c>/<c>scope</c> policy,
    ///     you may short-circuit to “issue downstream authorization code” (not implemented here; controller would then redirect to client with <c>code+state</c>).
    ///     Otherwise continue with upstream authorization.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Persist downstream login transaction</b> (<c>login_transaction</c>): store normalized parameters and the client PKCE challenge.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Derive upstream request</b> and <b>persist</b> (<c>login_transaction_upstream</c>): generate <c>upstream_state</c>,
    ///     <c>upstream_nonce</c>, <c>upstream_code_verifier</c>/<c>challenge</c>, and compose the absolute upstream authorize URL.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Routing</b>: choose upstream IdP (ID-porten default, Feide/Test-IDP based on <c>acr_values</c>, scope and allowlists/feature flags).</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Return</b> <see cref="AuthorizeResult"/> with <see cref="AuthorizeResultKind.RedirectUpstream"/> and any cookies to set (e.g., request correlation, CSRF, login txn cookie).</description>
    ///   </item>
    /// </list>
    ///
    /// <h2>Error handling</h2>
    /// <list type="bullet">
    ///   <item><description>If the <c>redirect_uri</c> is valid for the client, return <see cref="AuthorizeResultKind.ErrorRedirectToClient"/>
    ///   with an OIDC-compliant <c>error</c>/<c>error_description</c> and echo original <c>state</c>.</description></item>
    ///   <item><description>If the <c>redirect_uri</c> cannot be trusted (unknown/mismatch), return <see cref="AuthorizeResultKind.LocalError"/> and render a safe local error page.</description></item>
    /// </list>
    ///
    /// <h2>Security</h2>
    /// <list type="bullet">
    ///   <item><description>Do not log secrets (code_verifier, tokens, PID). Use correlation IDs.</description></item>
    ///   <item><description>Enforce PKCE S256; require exact redirect URI match; enforce scope policy and nonce (recommended).</description></item>
    ///   <item><description>Short TTLs on transactions; set <c>Cache-Control: no-store</c> at the controller.</description></item>
    /// </list>
    ///
    /// <h2>Thread-safety</h2>
    /// The service should be stateless and thread-safe; all state goes to the database and cookies returned via <see cref="AuthorizeResult.Cookies"/>.
    /// </remarks>
    public class OidcServerService : IOidcServerService
    {
        /// <summary>
        /// Handles an incoming OIDC <c>/authorize</c> request and returns a high-level outcome that the controller converts to HTTP.
        /// </summary>
        /// <param name="request">
        /// Normalized authorize request (already parsed/split by controller/binder/mapper). Must include:
        /// <list type="bullet">
        ///   <item><description><c>ResponseType</c> = <c>code</c></description></item>
        ///   <item><description><c>ClientId</c>, absolute <c>RedirectUri</c></description></item>
        ///   <item><description><c>Scopes</c> (must contain <c>openid</c>)</description></item>
        ///   <item><description><c>CodeChallenge</c> and <c>CodeChallengeMethod=S256</c></description></item>
        ///   <item><description>Optional: <c>State</c>, <c>Nonce</c>, <c>AcrValues</c>, <c>Prompts</c>, <c>UiLocales</c>, <c>MaxAge</c>, <c>RequestUri</c> (PAR), <c>RequestObject</c> (JAR)</description></item>
        /// </list>
        /// </param>
        /// <returns>
        /// An <see cref="AuthorizeResult"/> instructing the controller to:
        /// <list type="bullet">
        ///   <item><description><b>Redirect upstream</b> (most common): <see cref="AuthorizeResultKind.RedirectUpstream"/> with absolute upstream URL and <c>UpstreamState</c>.</description></item>
        ///   <item><description><b>Error redirect to client</b>: <see cref="AuthorizeResultKind.ErrorRedirectToClient"/> with OIDC error and echo <c>state</c>.</description></item>
        ///   <item><description><b>Local error</b>: <see cref="AuthorizeResultKind.LocalError"/> when we cannot safely redirect.</description></item>
        ///   <item><description><b>Render interaction</b>: <see cref="AuthorizeResultKind.RenderInteraction"/> when consent/actor selection is required.</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
        /// <remarks>
        /// <h3>Algorithm (happy path)</h3>
        /// <ol>
        ///   <li>Validate required fields and formats (response_type, redirect_uri absolute, S256, scope includes openid, etc.).</li>
        ///   <li>Lookup client; verify exact redirect URI; enforce client policy (allowed_scopes).</li>
        ///   <li>If <c>request.RequestUri</c> set: resolve PAR, override parameters, and re-validate.</li>
        ///   <li>If valid IdP session exists and policy allows reuse:
        ///       either return a special “issue downstream code” result (future extension) or continue to upstream if <c>prompt=login</c> or <c>max_age</c> expired.</li>
        ///   <li>Persist <c>login_transaction</c> row with downstream parameters and client PKCE challenge.</li>
        ///   <li>Choose upstream (ID-porten default; Feide/Test-IDP based on <c>acr_values</c>/scope/allowlist).</li>
        ///   <li>Generate <c>upstream_state</c>, <c>upstream_nonce</c>, <c>upstream_code_verifier</c> → compute <c>upstream_code_challenge</c>.</li>
        ///   <li>Persist <c>login_transaction_upstream</c> with generated values and callback binding.</li>
        ///   <li>Compose absolute upstream authorize URL and return <see cref="AuthorizeResultKind.RedirectUpstream"/> + any cookies to set.</li>
        /// </ol>
        ///
        /// <h3>Error cases</h3>
        /// <ul>
        ///   <li>Unknown client, bad redirect, missing PKCE ⇒ <see cref="AuthorizeResultKind.ErrorRedirectToClient"/> if safe, else <see cref="AuthorizeResultKind.LocalError"/>.</li>
        ///   <li>Invalid scope (<c>openid</c> missing) ⇒ error redirect <c>invalid_scope</c>.</li>
        ///   <li>Unsupported response_type ⇒ <c>unsupported_response_type</c>.</li>
        /// </ul>
        /// </remarks>
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

            //// ui_locales & acr_values: space-separated tokens with safe charset
            //if (!AreSpaceSeparatedTokensValid(request.UiLocales))
            //{
            //    return Fail("invalid_request", "ui_locales contains invalid characters.");
            //}

            //if (!AreSpaceSeparatedTokensValid(request.AcrValues))
            //{
            //    return Fail("invalid_request", "acr_values contains invalid characters.");
            //}

            // nonce recommended (optionally enforce)
            // if (string.IsNullOrWhiteSpace(request.Nonce)) return Fail("invalid_request", "nonce is required.");

            // If we reach here, basic validation passed.
            // ... continue with client lookup, redirect_uri exact-match, PKCE storage, etc.
            // (Return a placeholder for now so code compiles; replace with your actual flow.)
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

    }
}
