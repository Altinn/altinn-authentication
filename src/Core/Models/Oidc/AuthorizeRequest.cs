using System;
using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Defines the parameters for an OpenID Connect (OIDC) <c>/authorize</c> request.
    /// </summary>
    /// <remarks>
    /// Based primarily on OIDC Core 1.0 §3.1.2.1 (Authorization Endpoint) and related specs:
    /// - OIDC Core 1.0: https://openid.net/specs/openid-connect-core-1_0.html
    /// - OAuth 2.1 / RFC 6749 (OAuth 2.0), RFC 7636 (PKCE), RFC 9207 (OAuth 2.0 Authorization Server Issuer Identification)
    /// - RFC 9101 (JAR – JWT-Secured Authorization Request), RFC 9126 (PAR – Pushed Authorization Requests)
    /// - RFC 8707 (Resource Indicators), RFC 9396 (RAR – Rich Authorization Requests)
    ///
    /// NOTE (Altinn): Some fields are not yet active in Altinn flows but are included for standards alignment and future use.
    /// </remarks>
    public sealed class AuthorizeRequest
    {
        /// <summary>
        /// The OAuth/OIDC response type to use.
        /// </summary>
        /// <value>Default is <c>"code"</c> for Authorization Code Flow.</value>
        /// <remarks>
        /// <b>OIDC reason:</b> Signals which flow is in use. In OIDC Core §3.1.2.1, <c>response_type=code</c> starts the Authorization Code Flow,
        /// returning an authorization code to the client via the <c>redirect_uri</c>.
        /// <para><b>Security:</b> Code flow (with PKCE) is recommended over implicit/hybrid to avoid token leakage in front channels.</para>
        /// <para><b>Altinn:</b> Altinn supports only Authorization Code with PKCE; value must be <c>code</c>.</para>
        /// </remarks>
        public string ResponseType { get; init; } = "code";

        /// <summary>
        /// The registered client identifier of the relying party (RP).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Used to look up the client’s metadata, allowed <c>redirect_uri</c> values, and policy (OIDC Core §2, §3.1.2.1).
        /// <para><b>Validation:</b> Must match a known client; otherwise, the request is invalid.</para>
        /// </remarks>
        public string? ClientId { get; init; } = default!;

        /// <summary>
        /// The exact redirect target for the authorization response.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Where the Authorization Server sends the user-agent after user interaction (OIDC Core §3.1.2.1).
        /// <para><b>Security:</b> Must exactly match a pre-registered URI for the <c>client_id</c> to prevent open-redirect attacks.</para>
        /// <para><b>Altinn:</b> Strict exact match required.</para>
        /// </remarks>
        public Uri RedirectUri { get; init; } = default!;

        /// <summary>
        /// The requested scopes.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Limits the access privileges being requested (OAuth 2.0 §3.3). OIDC requires including <c>openid</c> to obtain an ID Token.
        /// <para><b>Examples:</b> <c>openid</c>, API/resource scopes, and optional <c>offline_access</c> (if refresh tokens are allowed).</para>
        /// <para><b>Altinn:</b> Scopes must be registered/allowed for the client. <c>openid</c> is required for OIDC.</para>
        /// </remarks>
        public string[] Scopes { get; init; } = [];

        /// <summary>
        /// Opaque value to maintain request state between the authorization request and the redirect response.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Mitigates CSRF and correlates request/response (OIDC Core §3.1.2.1).
        /// <para><b>Security:</b> Should be unguessable; the RP must verify the same value is returned.</para>
        /// </remarks>
        public string? State { get; init; }

        /// <summary>
        /// Opaque value bound to the client session to associate with the resulting ID Token.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Mitigates token replay and “mix-up” attacks; enables nonce validation in the ID Token (OIDC Core §3.1.2.1, §15.5.2).
        /// <para><b>Usage:</b> Required when an ID Token is returned (i.e., OIDC). The client must verify the <c>nonce</c> in the ID Token.</para>
        /// </remarks>
        public string? Nonce { get; init; }

        /// <summary>
        /// PKCE code challenge derived from a high-entropy <c>code_verifier</c>.
        /// </summary>
        /// <remarks>
        /// <b>OIDC/OAuth reason:</b> PKCE (RFC 7636) binds the authorization code to a client-held secret (<c>code_verifier</c>) to prevent code interception.
        /// <para><b>Altinn:</b> Required for public/native/Spa clients; recommended for all clients.</para>
        /// </remarks>
        public string? CodeChallenge { get; init; } = default!;

        /// <summary>
        /// The PKCE code challenge method. 
        /// </summary>
        /// <value>Default is <c>S256</c>.</value>
        /// <remarks>
        /// <b>OIDC/OAuth reason:</b> RFC 7636 allows <c>plain</c> and <c>S256</c>; <c>S256</c> is the recommended method.
        /// <para><b>Altinn:</b> <c>S256</c> is required.</para>
        /// </remarks>
        public string CodeChallengeMethod { get; init; } = "S256";

        /// <summary>
        /// Requested Authentication Context Class References.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Allows the RP to request specific authentication assurance levels/contexts (OIDC Core §1, §5.5.1).
        /// <para><b>Examples:</b> LoA values defined by national or organizational profiles.</para>
        /// <para><b>Altinn:</b> Used to steer upstream IdP selection or required assurance level when applicable.</para>
        /// </remarks>
        public string[] AcrValues { get; init; } = [];

        /// <summary>
        /// Prompts that influence user interaction at the Authorization Server (e.g., <c>login</c>, <c>consent</c>, <c>select_account</c>, <c>none</c>).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Controls whether to force re-authentication or consent, or to suppress UI if possible (OIDC Core §3.1.2.1).
        /// <para><b>Altinn:</b> Currently not used; reserved for future UI/UX control.</para>
        /// </remarks>
        public string[] Prompts { get; init; } = [];

        /// <summary>
        /// Requested user interface locales in BCP47 language tags, ordered by preference.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Enables UI localization (OIDC Core §3.1.2.1).
        /// <para><b>Altinn:</b> May be used to localize login/consent pages when supported.</para>
        /// </remarks>
        public string[] UiLocales { get; init; } = [];

        /// <summary>
        /// Maximum acceptable authentication age in seconds.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Ensures recent user authentication (OIDC Core §3.1.2.1, §3.1.2.2). If exceeded, the AS should re-authenticate the user.
        /// <para><b>Altinn:</b> Not currently enforced; reserved for future policy.</para>
        /// </remarks>
        public int? MaxAge { get; init; }

        /// <summary>
        /// A reference to a previously registered or retrievable request object (by URI).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> OIDC allows passing parameters by reference via <c>request_uri</c> (OIDC Core §6). Frequently used with PAR (RFC 9126).
        /// <para><b>Security:</b> Must be hosted at or pushed to a trusted location; often combined with JAR/PAR to protect request integrity.</para>
        /// <para><b>Altinn:</b> Supported for standards alignment when using PAR/JAR patterns.</para>
        /// </remarks>
        public string? RequestUri { get; init; }

        /// <summary>
        /// A JWT-secured authorization request object (JAR).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Encapsulates request parameters in a signed (and optionally encrypted) JWT (OIDC Core §6; RFC 9101).
        /// <para><b>Security:</b> Provides tamper-proof parameters and non-repudiation; commonly combined with PAR.</para>
        /// <para><b>Altinn:</b> Not yet supported end-to-end; reserved for future interoperability.</para>
        /// </remarks>
        public string? RequestObject { get; init; }

        /// <summary>
        /// Response serialization preference for the authorization response.
        /// </summary>
        /// <remarks>
        /// <b>OIDC/OAuth reason:</b> Controls whether parameters are returned in query, fragment, or form_post (OIDC Core §3.1.2.5 and Response Mode spec).
        /// <para><b>Typical:</b> <c>query</c> for code flow; <c>form_post</c> enhances size and security versus URL lengths.</para>
        /// <para><b>Altinn:</b> Not currently changeable by clients; defaults follow code flow best practices.</para>
        /// </remarks>
        public string? ResponseMode { get; init; }

        /// <summary>
        /// Hint for the username/identifier to pre-fill the login UI.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Helps user selection (OIDC Core §3.1.2.1). Typically an email, phone, or subject hint.
        /// <para><b>Altinn:</b> Not currently used to influence UI.</para>
        /// </remarks>
        public string? LoginHint { get; init; }

        /// <summary>
        /// A recent ID Token used to hint which user is being targeted.
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> <c>id_token_hint</c> can drive account selection or logout flows (OIDC Core §3.1.2.1, §3.1.2.2, and RP-Initiated Logout profiles).
        /// <para><b>Altinn:</b> NOT IN USE:Not currently processed.</para>
        /// </remarks>
        public string? IdTokenHint { get; init; }

        /// <summary>
        /// The OIDC "claims" parameter JSON (per-user or per-ID-Token/Access-Token claim requests).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> Fine-grained claim requests for <c>id_token</c> and <c>userinfo</c> (OIDC Core §5.5).
        /// <para><b>Format:</b> JSON object specifying essential/voluntary claims and <c>acr</c> requirements.</para>
        /// <para><b>Altinn:</b> NOT IN USE: Parsed when present; unsupported claims are ignored or rejected per policy.</para>
        /// </remarks>
        public string? ClaimsJson { get; init; }

        /// <summary>
        /// Preferred locales for individual claim values (BCP47).
        /// </summary>
        /// <remarks>
        /// <b>OIDC reason:</b> NOT IN USE: Allows localized claim values (OIDC Core §5.2, §5.5).</remarks>
        public string? ClaimsLocales { get; init; }

        /// <summary>
        /// Rich Authorization Requests (RAR) payload, if used.
        /// </summary>
        /// <remarks>
        /// <b>OAuth reason:</b> RFC 9396 defines <c>authorization_details</c> for structured, fine-grained permissions (e.g., data categories, actions).
        /// <para><b>Altinn:</b> NOT IN USE : Reserved for scenarios requiring granular consent beyond simple scopes.</para>
        /// </remarks>
        public string? AuthorizationDetailsJson { get; init; }

        /// <summary>
        /// Target resource indicator for issued tokens.
        /// </summary>
        /// <remarks>
        /// <b>OAuth reason:</b> RFC 8707 allows the client to indicate the resource server audience distinct from the AS.
        /// <para><b>Altinn:</b> NOT IN USE</para>
        /// </remarks>
        public string? Resource { get; init; }

        /// <summary>
        /// The caller's IP address as observed by the front end.
        /// </summary>
        /// <remarks>
        /// <b>Standard status:</b> Not an OIDC parameter. Platform telemetry only (for risk, logging, throttling).</remarks>
        public IPAddress? ClientIp { get; init; }

        /// <summary>
        /// A hashed user-agent fingerprint for correlation and risk signals.
        /// </summary>
        /// <remarks>
        /// <b>Standard status:</b> Not an OIDC parameter. Platform telemetry only.</remarks>
        public string? UserAgentHash { get; init; }

        /// <summary>
        /// Correlation identifier for distributed tracing across components.
        /// </summary>
        /// <remarks>
        /// <b>Standard status:</b> Not an OIDC parameter. Operational telemetry only; echoed in logs/diagnostics.</remarks>
        public Guid? CorrelationId { get; init; }
    }
}
