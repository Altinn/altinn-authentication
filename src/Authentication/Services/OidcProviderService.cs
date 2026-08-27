#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Telemetry;
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

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly Metrics _metrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcProviderService"/> class.
        /// </summary>
        public OidcProviderService(HttpClient httpClient, ILogger<OidcProviderService> logger, IMetricsProvider metricsProvider)
        {
            _httpClient = httpClient;
            _logger = logger;
            _metrics = metricsProvider.Get<Metrics>();
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

            // Client secret. Set if configured
            if (!string.IsNullOrEmpty(provider.ClientSecret))
            {
                kvps.Add("client_secret", provider.ClientSecret);
            }

            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                kvps.Add("code_verifier", codeVerifier);
            }

            FormUrlEncodedContent formUrlEncodedContent = new(kvps);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(provider.TokenEndpoint, formUrlEncodedContent, cancellationToken);
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

            public static Metrics Create(Meter meter) => new(meter);

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
