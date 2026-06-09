#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Mints and validates the self-identified account-link token (issue #2035) using a dedicated
    /// certificate, kept separate from the OIDC signing keys so the token can never be used for
    /// authentication.
    /// </summary>
    public class SelfIdentifiedLinkTokenService : ISelfIdentifiedLinkTokenService
    {
        /// <summary>Custom claim type carrying the authenticated requester's user id.</summary>
        public const string SourceUserIdClaim = "source_user_id";

        /// <summary>Custom claim type carrying the party UUID of the self-identified user being claimed.</summary>
        public const string TargetPartyUuidClaim = "target_party_uuid";

        /// <summary>Claim type identifying the token's single purpose.</summary>
        public const string PurposeClaim = "purpose";

        /// <summary>The only accepted value for the <see cref="PurposeClaim"/>.</summary>
        public const string PurposeValue = "si-account-link";

        private readonly ISelfIdentifiedLinkTokenCertificateProvider _certificateProvider;
        private readonly SelfIdentifiedLinkTokenSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SelfIdentifiedLinkTokenService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfIdentifiedLinkTokenService"/> class.
        /// </summary>
        public SelfIdentifiedLinkTokenService(
            ISelfIdentifiedLinkTokenCertificateProvider certificateProvider,
            IOptions<SelfIdentifiedLinkTokenSettings> settings,
            TimeProvider timeProvider,
            ILogger<SelfIdentifiedLinkTokenService> logger)
        {
            _certificateProvider = certificateProvider;
            _settings = settings.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> MintAsync(int sourceUserId, Guid targetPartyUuid, CancellationToken cancellationToken = default)
        {
            X509Certificate2 certificate = await GetSigningCertificate();

            DateTimeOffset now = _timeProvider.GetUtcNow();

            List<Claim> claims =
            [
                new(PurposeClaim, PurposeValue),
                new(SourceUserIdClaim, sourceUserId.ToString(CultureInfo.InvariantCulture)),
                new(TargetPartyUuidClaim, targetPartyUuid.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ];

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.AddMinutes(_settings.LifetimeMinutes).UtcDateTime,
                SigningCredentials = new X509SigningCredentials(certificate),
            };

            JwtSecurityTokenHandler tokenHandler = new();
            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <inheritdoc/>
        public async Task<SelfIdentifiedLinkTokenResult> ValidateAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return SelfIdentifiedLinkTokenResult.Invalid("Token is empty.");
            }

            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();
            List<SecurityKey> signingKeys = certificates.Select(c => (SecurityKey)new X509SecurityKey(c)).ToList();

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_settings.ClockSkewSeconds),

                // Validate lifetime against the injected TimeProvider so behaviour is deterministic
                // and consistent with minting.
                LifetimeValidator = (notBefore, expires, _, parameters) =>
                {
                    if (expires is null)
                    {
                        return false;
                    }

                    DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    if (notBefore is { } nbf && nowUtc + parameters.ClockSkew < nbf)
                    {
                        return false;
                    }

                    return nowUtc - parameters.ClockSkew <= expires;
                },
            };

            JwtSecurityTokenHandler tokenHandler = new();

            // Keep the original (short) claim names instead of the inbound URI mapping.
            tokenHandler.InboundClaimTypeMap.Clear();

            try
            {
                tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                JwtSecurityToken jwt = (JwtSecurityToken)validatedToken;

                string? purpose = jwt.Claims.FirstOrDefault(c => c.Type == PurposeClaim)?.Value;
                if (!string.Equals(purpose, PurposeValue, StringComparison.Ordinal))
                {
                    return SelfIdentifiedLinkTokenResult.Invalid("Token purpose mismatch.");
                }

                if (!TryGetIntClaim(jwt, SourceUserIdClaim, out int sourceUserId) ||
                    !TryGetGuidClaim(jwt, TargetPartyUuidClaim, out Guid targetPartyUuid))
                {
                    return SelfIdentifiedLinkTokenResult.Invalid("Token is missing required source/target claims.");
                }

                string? jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                return SelfIdentifiedLinkTokenResult.Valid(sourceUserId, targetPartyUuid, jti);
            }
            catch (SecurityTokenException ex)
            {
                // Expected for expired / tampered / wrong-audience tokens. Do not log the token.
                _logger.LogInformation("Self-identified link token rejected: {Reason}", ex.GetType().Name);
                return SelfIdentifiedLinkTokenResult.Invalid("Token validation failed.");
            }
            catch (ArgumentException)
            {
                // Malformed token string.
                return SelfIdentifiedLinkTokenResult.Invalid("Token is malformed.");
            }
        }

        private async Task<X509Certificate2> GetSigningCertificate()
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            X509Certificate2? certificate = certificates
                .Where(c => c.HasPrivateKey)
                .OrderByDescending(c => c.NotBefore)
                .FirstOrDefault();

            return certificate
                ?? throw new InvalidOperationException("No self-identified link-token signing certificate with a private key is available.");
        }

        private static bool TryGetIntClaim(JwtSecurityToken jwt, string claimType, out int value)
        {
            string? raw = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetGuidClaim(JwtSecurityToken jwt, string claimType, out Guid value)
        {
            string? raw = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            return Guid.TryParse(raw, out value);
        }
    }
}
