#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Provides functionality to validate JSON Web Tokens (JWTs) issued by an upstream OpenID Connect (OIDC) provider.
    /// </summary>
    /// <remarks>This class is responsible for validating the authenticity and integrity of JWTs by verifying
    /// their signatures against the signing keys retrieved from the upstream OIDC provider. It ensures that the token
    /// is valid, has not expired, and adheres to the expected security parameters.</remarks>
    public class UpstreamTokenValidator(ILogger<UpstreamTokenValidator> logger, ISigningKeysRetriever signingKeysRetriever, IMetricsProvider metricsProvider) : IUpstreamTokenValidator
    {
        private readonly JwtSecurityTokenHandler _validator = new();
        private readonly ISigningKeysRetriever _signingKeysRetriever = signingKeysRetriever;
        private readonly ILogger<UpstreamTokenValidator> _logger = logger;
        private readonly Metrics _metrics = metricsProvider.Get<Metrics>();

        /// <summary>
        /// Validate the token issued by an upstream OIDC provider.
        /// </summary>
        public async Task<JwtSecurityToken> ValidateTokenAsync(string token, OidcProvider provider, string? nonce, CancellationToken cancellationToken = default)
        {
            string providerKey = provider.IssuerKey ?? provider.Issuer;

            // The caller only supplies a nonce for the ID token, so it doubles as the token discriminator.
            string tokenType = nonce is null ? Metrics.TokenTypeAccessToken : Metrics.TokenTypeIdToken;

            if (string.IsNullOrEmpty(token))
            {
                _metrics.TokenValidation(providerKey, tokenType, Metrics.OutcomeMissingToken);
                throw new ArgumentException("Token must be provided.", nameof(token));
            }

            ICollection<SecurityKey> signingKeys;
            try
            {
                signingKeys = await _signingKeysRetriever.GetSigningKeys(provider.WellKnownConfigEndpoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Discovery/JWKS is unreachable. This takes sign-in down just as effectively as a
                // rejected token call, but for an entirely different reason - keep it distinguishable.
                _metrics.TokenValidation(providerKey, tokenType, Metrics.OutcomeSigningKeysUnavailable);
                _logger.LogError(ex, "Could not retrieve signing keys for {Provider} from {WellKnownEndpoint}", providerKey, provider.WellKnownConfigEndpoint);
                throw;
            }

            try
            {
                JwtSecurityToken jwtToken = ValidateToken(token, provider.Issuer, signingKeys);
                if (nonce != null)
                {
                    // Only relevant for ID tokens
                    ValidateNonce(jwtToken, nonce);
                }

                _metrics.TokenValidation(providerKey, tokenType, Metrics.OutcomeSuccess);
                return jwtToken;
            }
            catch (Exception ex)
            {
                _metrics.TokenValidation(providerKey, tokenType, ClassifyValidationFailure(ex));
                throw;
            }
        }

        /// <summary>
        /// Maps a validation exception to a bounded set of metric outcomes. Anything unrecognised becomes
        /// <c>other</c> so the <c>outcome</c> dimension stays small enough to alert and split on.
        /// </summary>
        private static string ClassifyValidationFailure(Exception exception) => exception switch
        {
            SecurityTokenSignatureKeyNotFoundException => Metrics.OutcomeSigningKeyNotFound,
            SecurityTokenInvalidSignatureException => Metrics.OutcomeInvalidSignature,
            SecurityTokenInvalidIssuerException => Metrics.OutcomeInvalidIssuer,
            SecurityTokenExpiredException or SecurityTokenNotYetValidException => Metrics.OutcomeExpired,
            SecurityTokenValidationException => Metrics.OutcomeInvalidToken,
            _ => Metrics.OutcomeOther,
        };

        private static string TrimEndSlash(string s) => s.EndsWith('/') ? s[..^1] : s;

        private static bool ConstantTimeEquals(string left, string right)
        {
            byte[] a = Encoding.UTF8.GetBytes(left);
            byte[] b = Encoding.UTF8.GetBytes(right);

            if (a.Length != b.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        private JwtSecurityToken ValidateToken(string originalToken, string expectedIssuer, ICollection<SecurityKey> signingKeys)
        {
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidateAudience = false,
                IssuerValidator = (tokenIssuer, securityToken, parameters) =>
                {
                    // Exact match is the spec requirement (OIDC Core).
                    if (string.Equals(tokenIssuer, expectedIssuer, StringComparison.Ordinal))
                    {
                        return tokenIssuer;
                    }

                    // Pragmatic allowance: treat trailing slash difference as equivalent.
                    // Useful when some upstreams include / omit trailing slash inconsistently.
                    if (TrimEndSlash(tokenIssuer).Equals(TrimEndSlash(expectedIssuer), StringComparison.Ordinal))
                    {
                        // Keep a breadcrumb that we normalized.
                        _logger.LogDebug("Issuer matched after trimming trailing slash: '{Actual}' ~ '{Expected}'", tokenIssuer, expectedIssuer);
                        return tokenIssuer;
                    }

                    _logger.LogWarning("Issuer mismatch. Expected '{Expected}', got '{Actual}'", expectedIssuer, tokenIssuer);
                    throw new SecurityTokenInvalidIssuerException($"Invalid issuer. Expected '{expectedIssuer}', got '{tokenIssuer}'.");
                },
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            _validator.ValidateToken(originalToken, validationParameters, out SecurityToken? validated);
            return (JwtSecurityToken)validated;
        }

        private void ValidateNonce(JwtSecurityToken token, string expectedNonce)
        {
            string? actual = token.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;

            if (string.IsNullOrEmpty(actual))
            {
                _logger.LogWarning("ID token missing 'nonce' claim.");
                throw new SecurityTokenValidationException("ID token missing 'nonce' claim.");
            }

            // Compare constant-time to avoid timing leaks.
            if (!ConstantTimeEquals(expectedNonce, actual))
            {
                _logger.LogWarning("ID token nonce mismatch.");
                throw new SecurityTokenValidationException("Invalid nonce.");
            }
        }

        private sealed class Metrics(Meter meter)
            : IMetrics<Metrics>
        {
            /// <summary>The token was signed by a known key, issued by the expected issuer, and live.</summary>
            public const string OutcomeSuccess = "success";

            /// <summary>Discovery / JWKS could not be reached, so nothing could be validated.</summary>
            public const string OutcomeSigningKeysUnavailable = "signing_keys_unavailable";

            /// <summary>The token was signed with a key not present in the provider's JWKS (key rollover).</summary>
            public const string OutcomeSigningKeyNotFound = "signing_key_not_found";

            /// <summary>The signature did not verify against the provider's keys.</summary>
            public const string OutcomeInvalidSignature = "invalid_signature";

            /// <summary>The <c>iss</c> claim did not match the configured issuer.</summary>
            public const string OutcomeInvalidIssuer = "invalid_issuer";

            /// <summary>The token was expired or not yet valid.</summary>
            public const string OutcomeExpired = "expired";

            /// <summary>Any other validation failure, including a missing or mismatched nonce.</summary>
            public const string OutcomeInvalidToken = "invalid_token";

            /// <summary>The upstream token exchange handed us nothing to validate.</summary>
            public const string OutcomeMissingToken = "missing_token";

            /// <summary>An unclassified failure.</summary>
            public const string OutcomeOther = "other";

            /// <summary>The upstream ID token.</summary>
            public const string TokenTypeIdToken = "id_token";

            /// <summary>The upstream access token.</summary>
            public const string TokenTypeAccessToken = "access_token";

            private readonly Counter<int> _tokenValidation
                = meter.CreateCounter<int>(
                        name: "altinn.authentication.oidc.upstream_token_validation",
                        description: "Outcome of validating tokens issued by the upstream OIDC provider");

            public static Metrics Create(Meter meter) => new(meter);

            /// <summary>
            /// Counts one upstream token validation. Successes are counted too, so an alert can be
            /// written on the failure <em>rate</em> rather than an absolute failure count.
            /// </summary>
            public void TokenValidation(string provider, string tokenType, string outcome)
                => _tokenValidation.Add(
                    1,
                    new KeyValuePair<string, object?>("provider", provider),
                    new KeyValuePair<string, object?>("token.type", tokenType),
                    new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
