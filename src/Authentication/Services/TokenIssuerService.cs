using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Token issuer service for minting OAuth 2.0 / OIDC tokens.
    /// </summary>
    public class TokenIssuerService(IJwtSigningCertificateProvider jwtSigningCertificateProvider, IOptions<GeneralSettings> generalSettings, TimeProvider timeProvider) : ITokenIssuer
    {
        private readonly IJwtSigningCertificateProvider _certificateProvider = jwtSigningCertificateProvider;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc/>
        public async Task<string> CreateAccessTokenAsync(ClaimsPrincipal principal, DateTimeOffset expires, CancellationToken ct = default)
        {
            string accessToken = await GenerateToken(principal, expires);
            return accessToken;
        }

        /// <inheritdoc/>
        public async Task<string> CreateIdTokenAsync(ClaimsPrincipal principal, OidcClient client, DateTimeOffset tokenExpiration, CancellationToken ct = default)
        {
            return await GenerateToken(principal, tokenExpiration);
        }

        private async Task<string> GenerateToken(ClaimsPrincipal principal, DateTimeOffset tokenExpiration)
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            X509Certificate2 certificate = GetLatestCertificateWithRolloverDelay(
                certificates, _generalSettings.JwtSigningCertificateRolloverDelayHours);

            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                IssuedAt = _timeProvider.GetUtcNow().UtcDateTime,
                NotBefore = _timeProvider.GetUtcNow().UtcDateTime,
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = tokenExpiration.UtcDateTime,
                SigningCredentials = new X509SigningCredentials(certificate)
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            string serializedToken = tokenHandler.WriteToken(token);

            return serializedToken;
        }

        private X509Certificate2 GetLatestCertificateWithRolloverDelay(
         List<X509Certificate2> certificates, int rolloverDelayHours)
        {
            // First limit the search to just those certificates that have existed longer than the rollover delay.
            var rolloverCutoff = _timeProvider.GetUtcNow().AddHours(-rolloverDelayHours);
            var potentialCerts =
                certificates.Where(c => c.NotBefore < rolloverCutoff).ToList();

            // If no certs could be found, then widen the search to any usable certificate.
            if (!potentialCerts.Any())
            {
                potentialCerts = certificates.Where(c => c.NotBefore < DateTime.Now).ToList();
            }

            // Of the potential certs, return the newest one.
            return potentialCerts
                .OrderByDescending(c => c.NotBefore)
                .FirstOrDefault();
        }
    }
}
