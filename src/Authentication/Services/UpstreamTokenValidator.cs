#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public class UpstreamTokenValidator(ILogger<UpstreamTokenValidator> logger, ISigningKeysRetriever signingKeysRetriever) : IUpstreamTokenValidator
    {
        private readonly JwtSecurityTokenHandler _validator = new();
        private readonly ISigningKeysRetriever _signingKeysRetriever = signingKeysRetriever;
        private readonly ILogger<UpstreamTokenValidator> _logger = logger;

        /// <summary>
        /// Validate the token issued by an upstream OIDC provider.
        /// </summary>
        public async Task<JwtSecurityToken> ValidateTokenAsync(string token, OidcProvider provider, string? nonce, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token must be provided.", nameof(token));
            }
            
            ICollection<SecurityKey> signingKeys = await _signingKeysRetriever.GetSigningKeys(provider.WellKnownConfigEndpoint);
            JwtSecurityToken jwtToken = ValidateToken(token, provider.Issuer, signingKeys);
            if (nonce != null)
            {
                // Only relevant for ID tokens
                ValidateNonce(jwtToken, nonce);
            }

            return jwtToken;
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

        private static string TrimEndSlash(string s) => s.EndsWith('/') ? s[..^1] : s;

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
    }
}
