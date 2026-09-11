#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Oidc provider for exchanging authorization code in token
    /// </summary>
    public class OidcProviderService : IOidcProvider
    {
        /// <summary>
        /// The OAuth 2.0 / OIDC error codes we are willing to put on the <c>error.type</c> metric dimension.
        /// Anything else is folded into <c>_OTHER</c>: the upstream controls this value, and an unbounded
        /// dimension would multiply the metric's time series (and the Application Insights bill).
        /// </summary>
        private static readonly FrozenSet<string> KnownErrorCodes = new[]
        {
            "invalid_request",
            "invalid_client",
            "invalid_grant",
            "unauthorized_client",
            "unsupported_grant_type",
            "invalid_scope",
            "server_error",
            "temporarily_unavailable",
        }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>
        /// Upper bound on how much of an error body we put in the log. During an upstream outage the
        /// body is often an HTML error page from an intermediary, and the first lines are enough.
        /// </summary>
        private const int MaxLoggedBodyLength = 512;

        /// <summary>Header carrying the DPoP proof (RFC 9449).</summary>
        private const string DpopHeader = "DPoP";

        /// <summary>Response header by which a provider supplies the nonce a proof must carry.</summary>
        private const string DpopNonceHeader = "DPoP-Nonce";

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly Metrics _metrics;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcProviderService"/> class.
        /// </summary>
        public OidcProviderService(HttpClient httpClient, ILogger<OidcProviderService> logger, IMetricsProvider metricsProvider, TimeProvider timeProvider)
        {
            _httpClient = httpClient;
            _logger = logger;
            _metrics = metricsProvider.Get<Metrics>();
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Performs a AccessToken Request as described in https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.3
        /// </summary>
        public async Task<OidcCodeResponse?> GetTokens(string authorizationCode, OidcProvider provider, string redirect_uri, string? codeVerifier, CancellationToken cancellationToken = default)
        {
            string providerKey = provider.IssuerKey ?? provider.Issuer;
            Dictionary<string, string> kvps = new Dictionary<string, string>();

            // REQUIRED.  The authorization code received from the authorization server.
            kvps.Add("code", authorizationCode);

            // REQUIRED, if the "redirect_uri" parameter was included in the
            // authorization request as described in Section 4.1.1, and their values MUST be identical.
            kvps.Add("redirect_uri", redirect_uri);

            // REQUIRED.  Value MUST be set to "authorization_code".
            kvps.Add("grant_type", "authorization_code");

            // REQUIRED.  Value MUST be set to "client_id".
            kvps.Add("client_id", provider.ClientId);

            // Client authentication. private_key_jwt takes precedence: a provider configured with
            // an assertion key has one because it does not accept a client secret at all, so
            // falling back to the secret would only produce invalid_client.
            AddClientAuthentication(kvps, provider);

            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                kvps.Add("code_verifier", codeVerifier);
            }

            HttpResponseMessage response;
            try
            {
                response = await SendTokenRequest(provider, kvps, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser went away. Not an upstream failure, so do not count it as one.
                throw;
            }
            catch (Exception ex)
            {
                // No HTTP response at all: DNS/TLS/connect failure, the resilience handler's request
                // timeout, or an open circuit breaker. The circuit-breaker exception type lives in
                // Polly, which we only have transitively, so this catch is deliberately broad - the
                // try block wraps nothing but the outbound call.
                _metrics.TokenExchange(providerKey, statusCode: null, errorType: ex.GetType().FullName);
                _logger.LogError(ex, "Upstream token request to {Provider} failed before a response was received", providerKey);
                return null;
            }

            using (response)
            {
                int statusCode = (int)response.StatusCode;
                string content = await response.Content.ReadAsStringAsync(cancellationToken);

                return response.IsSuccessStatusCode
                    ? ReadSuccessResponse(content, providerKey, statusCode)
                    : ReadErrorResponse(content, providerKey, statusCode);
            }
        }

        /// <summary>
        /// Sends the token request, adding a DPoP proof when the provider requires one and
        /// retrying once if the provider answers with a nonce challenge.
        /// </summary>
        /// <remarks>
        /// RFC 9449 lets the provider demand that proofs carry a server-chosen nonce, which it
        /// supplies only by rejecting a first attempt with <c>use_dpop_nonce</c> and a
        /// <c>DPoP-Nonce</c> header. A single retry is therefore part of the normal flow rather
        /// than error handling. It is bounded at one: a provider that keeps challenging is
        /// misbehaving, and retrying further would just hold the user's sign-in open.
        /// </remarks>
        private async Task<HttpResponseMessage> SendTokenRequest(OidcProvider provider, Dictionary<string, string> body, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await SendTokenRequestOnce(provider, body, nonce: null, cancellationToken);

            if (!provider.UseDpop || response.StatusCode != HttpStatusCode.BadRequest)
            {
                return response;
            }

            string? nonce = response.Headers.TryGetValues(DpopNonceHeader, out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(nonce))
            {
                return response;
            }

            // Counted, not just logged. The first 400 is discarded and the outcome counter only
            // records the retry, so without this the challenge is invisible — and how often a
            // provider challenges is exactly what tells you whether the retry path is healthy.
            string providerKey = provider.IssuerKey ?? provider.Issuer;
            _metrics.DpopNonceChallenge(providerKey);
            _logger.LogDebug("Provider {Provider} requested a DPoP nonce; retrying the token request once", providerKey);
            response.Dispose();

            // The assertion is single-use as well, so the retry needs a fresh one alongside the
            // fresh proof.
            Dictionary<string, string> retryBody = new(body);
            AddClientAuthentication(retryBody, provider);

            return await SendTokenRequestOnce(provider, retryBody, nonce, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendTokenRequestOnce(OidcProvider provider, Dictionary<string, string> body, string? nonce, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, provider.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(body),
            };

            if (provider.UseDpop)
            {
                request.Headers.TryAddWithoutValidation(
                    DpopHeader,
                    DpopProofBuilder.Build(provider, HttpMethod.Post.Method, provider.TokenEndpoint, _timeProvider.GetUtcNow(), nonce));
            }

            return await _httpClient.SendAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<PushedAuthorizationResponse?> PushAuthorizationRequest(OidcProvider provider, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            string providerKey = provider.IssuerKey ?? provider.Issuer;

            Dictionary<string, string> body = new(parameters);
            AddClientAuthentication(body, provider);

            HttpResponseMessage response;
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Post, provider.PushedAuthorizationRequestEndpoint)
                {
                    Content = new FormUrlEncodedContent(body),
                };

                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.PushedAuthorizationRequest(providerKey, statusCode: null, errorType: ex.GetType().FullName);
                _logger.LogError(ex, "Pushed authorization request to {Provider} failed before a response was received", providerKey);
                return null;
            }

            using (response)
            {
                int statusCode = (int)response.StatusCode;
                string content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Oauth2ErrorResponse? error = TryReadOAuthError(content);
                    string errorType = error?.Error is { Length: > 0 } code && KnownErrorCodes.Contains(code)
                        ? code
                        : Metrics.ErrorTypeOther;

                    _metrics.PushedAuthorizationRequest(providerKey, statusCode, errorType);
                    _logger.LogError(
                        "Pushed authorization request to {Provider} was refused with {StatusCode}: {Error} {ErrorDescription}. Body: {Body}",
                        providerKey,
                        statusCode,
                        error?.Error,
                        error?.ErrorDescription,
                        Truncate(error is null ? content : null));

                    return null;
                }

                PushedAuthorizationResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<PushedAuthorizationResponse>(content);
                }
                catch (JsonException ex)
                {
                    _metrics.PushedAuthorizationRequest(providerKey, statusCode, Metrics.ErrorTypeInvalidResponse);
                    _logger.LogError(ex, "Pushed authorization request to {Provider} answered {StatusCode} with a body that is not JSON", providerKey, statusCode);
                    return null;
                }

                // A 2xx with no usable request_uri is not something to redirect on. The body is
                // deliberately not logged: it carries the reference, which is a credential.
                if (string.IsNullOrWhiteSpace(parsed?.RequestUri) || parsed.ExpiresIn <= 0)
                {
                    _metrics.PushedAuthorizationRequest(providerKey, statusCode, Metrics.ErrorTypeInvalidResponse);
                    _logger.LogError(
                        "Pushed authorization request to {Provider} answered {StatusCode} without a usable request_uri or expires_in",
                        providerKey,
                        statusCode);
                    return null;
                }

                _metrics.PushedAuthorizationRequest(providerKey, statusCode, errorType: null);
                return parsed;
            }
        }

        /// <summary>
        /// Adds client authentication to an outgoing form body: a client assertion when the
        /// provider is configured for one, otherwise the client secret.
        /// </summary>
        /// <remarks>
        /// Shared by the token request and the pushed authorization request so the two cannot
        /// drift apart. A fresh assertion is built per call — the <c>jti</c> is single-use, so the
        /// assertion pushed with PAR must not be reused when the code is exchanged.
        /// </remarks>
        private void AddClientAuthentication(Dictionary<string, string> body, OidcProvider provider)
        {
            body["client_id"] = provider.ClientId;

            if (ClientAssertionBuilder.IsConfiguredFor(provider))
            {
                body["client_assertion_type"] = ClientAssertionBuilder.ClientAssertionType;
                body["client_assertion"] = ClientAssertionBuilder.Build(provider, _timeProvider.GetUtcNow());
            }
            else if (!string.IsNullOrEmpty(provider.ClientSecret))
            {
                body["client_secret"] = provider.ClientSecret;
            }
        }

        /// <summary>
        /// Reads an OAuth 2.0 error response (RFC 6749 section 5.2). Returns <c>null</c> when the body
        /// is not one - during an outage it is often an HTML page from an intermediary rather than
        /// JSON from the OP, and <see cref="JsonSerializer"/> throws on that.
        /// </summary>
        private static Oauth2ErrorResponse? TryReadOAuthError(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Oauth2ErrorResponse>(content);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Truncate(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= MaxLoggedBodyLength ? value : string.Concat(value.AsSpan(0, MaxLoggedBodyLength), "...");
        }

        private OidcCodeResponse? ReadSuccessResponse(string content, string providerKey, int statusCode)
        {
            OidcCodeResponse? codeResponse = null;
            try
            {
                codeResponse = JsonSerializer.Deserialize<OidcCodeResponse>(content);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Upstream {Provider} returned {StatusCode} with a body that is not a valid token response", providerKey, statusCode);
            }

            if (codeResponse is null || string.IsNullOrEmpty(codeResponse.IdToken))
            {
                _metrics.TokenExchange(providerKey, statusCode, Metrics.ErrorTypeInvalidResponse);
                _logger.LogError("Upstream {Provider} returned {StatusCode} without a usable id_token", providerKey, statusCode);
                return null;
            }

            _metrics.TokenExchange(providerKey, statusCode, errorType: null);
            return codeResponse;
        }

        private OidcCodeResponse? ReadErrorResponse(string content, string providerKey, int statusCode)
        {
            Oauth2ErrorResponse? oauthError = TryReadOAuthError(content);
            string? error = oauthError?.Error;
            string errorType = error is not null && KnownErrorCodes.Contains(error) ? error : Metrics.ErrorTypeOther;

            _metrics.TokenExchange(providerKey, statusCode, errorType);

            // invalid_grant is routinely user-driven (back button, replayed or expired code), so a single
            // occurrence is not an incident. Everything else points at us or at the upstream.
            LogLevel level = string.Equals(errorType, "invalid_grant", StringComparison.Ordinal) ? LogLevel.Warning : LogLevel.Error;

            if (error is null)
            {
                _logger.Log(
                    level,
                    "Upstream token exchange with {Provider} failed with {StatusCode}. Body was not an OAuth error response: {Body}",
                    providerKey,
                    statusCode,
                    Truncate(content));
            }
            else
            {
                _logger.Log(
                    level,
                    "Upstream token exchange with {Provider} failed with {StatusCode} {ErrorCode}: {ErrorDescription}",
                    providerKey,
                    statusCode,
                    error,
                    Truncate(oauthError!.ErrorDescription)); // non-null whenever error is
            }

            return null;
        }

        /// <summary>
        /// An OAuth 2.0 error response body (RFC 6749 section 5.2).
        /// </summary>
        private sealed record Oauth2ErrorResponse
        {
            [JsonPropertyName("error")]
            public string? Error { get; init; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; init; }
        }

        private sealed class Metrics(Meter meter)
            : IMetrics<Metrics>
        {
            /// <summary>
            /// The OTel fallback when the failure has no low-cardinality name of its own — an upstream
            /// error code outside <see cref="KnownErrorCodes"/>, or a body that is not an OAuth error
            /// response at all. The accompanying <c>http.response.status_code</c> narrows it down.
            /// </summary>
            public const string ErrorTypeOther = "_OTHER";

            /// <summary>The upstream answered 2xx, but not with a usable token response.</summary>
            public const string ErrorTypeInvalidResponse = "invalid_response";

            private readonly Counter<int> _tokenExchange
                = meter.CreateCounter<int>(
                        name: "altinn.authentication.oidc.upstream_token_exchange",
                        description: "Authorization-code-to-token requests against the upstream OIDC provider");

            private readonly Counter<int> _pushedAuthorizationRequest
                = meter.CreateCounter<int>(
                        name: "altinn.authentication.oidc.upstream_pushed_authorization_request",
                        description: "Pushed authorization requests against the upstream OIDC provider");

            private readonly Counter<int> _dpopNonceChallenge
                = meter.CreateCounter<int>(
                        name: "altinn.authentication.oidc.upstream_dpop_nonce_challenge",
                        description: "Token requests the upstream provider answered with a DPoP nonce challenge, prompting one retry");

            public static Metrics Create(Meter meter) => new(meter);

            /// <summary>
            /// Counts one DPoP nonce challenge. The challenge is part of the normal flow, so this
            /// is not an error count — but it is the only way to see the retry path at all, since
            /// the discarded first response never reaches <see cref="TokenExchange"/>.
            /// </summary>
            /// <remarks>
            /// Read this with care: the last nonce is not yet kept between requests, as RFC 9449
            /// section 8 recommends, so a provider that issues nonces challenges <em>every</em>
            /// token request. Expect one challenge per sign-in until that is added. A rate above
            /// that is the signal worth alerting on.
            /// </remarks>
            public void DpopNonceChallenge(string provider)
            {
                TagList tags = default;
                tags.Add("provider", provider);
                _dpopNonceChallenge.Add(1, tags);
            }

            /// <summary>
            /// Counts one pushed authorization request, on the same convention as
            /// <see cref="TokenExchange"/>: successes counted too, so an alert can be written on
            /// the failure rate. Kept as its own instrument because a PAR failure and a token
            /// failure mean different things — the first stops a sign-in before the user reaches
            /// the provider, the second after they have authenticated there.
            /// </summary>
            /// <param name="provider">The configured provider key, e.g. <c>helseid</c>.</param>
            /// <param name="statusCode">The upstream HTTP status, or <c>null</c> when no response was received.</param>
            /// <param name="errorType">The failure classification, or <c>null</c> on success.</param>
            public void PushedAuthorizationRequest(string provider, int? statusCode, string? errorType)
            {
                TagList tags = default;
                tags.Add("provider", provider);

                if (statusCode is not null)
                {
                    tags.Add("http.response.status_code", statusCode.Value);
                }

                if (errorType is not null)
                {
                    tags.Add("error.type", errorType);
                }

                _pushedAuthorizationRequest.Add(1, tags);
            }

            /// <summary>
            /// Counts one authorization-code-to-token request. Successes are counted too, so that an
            /// alert can be written on the failure <em>rate</em> rather than an absolute failure count;
            /// a success is a measurement with no <c>error.type</c>, per the OpenTelemetry convention.
            /// </summary>
            /// <param name="provider">The configured provider key, e.g. <c>idporten</c>.</param>
            /// <param name="statusCode">The upstream HTTP status, or <c>null</c> when no response was received.</param>
            /// <param name="errorType">The failure classification, or <c>null</c> on success.</param>
            public void TokenExchange(string provider, int? statusCode, string? errorType)
            {
                TagList tags = default;
                tags.Add("provider", provider);

                if (statusCode is not null)
                {
                    tags.Add("http.response.status_code", statusCode.Value);
                }

                if (errorType is not null)
                {
                    tags.Add("error.type", errorType);
                }

                _tokenExchange.Add(1, tags);
            }
        }
    }
}
