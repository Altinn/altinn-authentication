using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using AltinnCore.Authentication.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Token issuer service for minting OAuth 2.0 / OIDC tokens.
    /// </summary>
    public class TokenIssuerService(IJwtSigningCertificateProvider jwtSigningCertificateProvider, IOptions<GeneralSettings> generalSettings) : ITokenIssuer
    {
        private readonly IJwtSigningCertificateProvider _certificateProvider = jwtSigningCertificateProvider;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;

        /// <inheritdoc/>
        public async Task<(string accessToken, DateTimeOffset expiresAt, string scope)> CreateAccessTokenAsync(AuthCodeRow code, DateTimeOffset now, CancellationToken ct = default)
        {
            ClaimsPrincipal principal = GetClaimsPrincipal(code);
            string accessToken = await GenerateToken(principal, now.AddMinutes(_generalSettings.JwtValidityMinutes).UtcDateTime);
            return (accessToken, now.AddMinutes(_generalSettings.JwtValidityMinutes), string.Join(" ", code.Scopes));
        }

        /// <inheritdoc/>
        public async Task<string> CreateIdTokenAsync(AuthCodeRow code, OidcClient client, DateTimeOffset now, CancellationToken ct = default)
        {
            ClaimsPrincipal principal = GetClaimsPrincipal(code, true);
            return await GenerateToken(principal, now.AddMinutes(_generalSettings.JwtValidityMinutes).UtcDateTime);
        }

        private ClaimsPrincipal GetClaimsPrincipal(AuthCodeRow authCodeRow, bool isIDToken = false)
        {
            List<Claim> claims = new()
            {
                new Claim("sub", authCodeRow.SubjectId),
                new Claim("sid", authCodeRow.SessionId),
                new Claim("iss", _generalSettings.PlatformEndpoint)
            };

            if (authCodeRow.SubjectPartyUuid != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, authCodeRow.SubjectPartyUuid.ToString()!));
            }

            if (authCodeRow.SubjectPartyId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, authCodeRow.SubjectPartyId.ToString()!));
            }

            if (authCodeRow.SubjectUserId != null)
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, authCodeRow.SubjectUserId.ToString()!));
            }

            if (authCodeRow.SubjectId != null)
            {
                claims.Add(new Claim("pid", authCodeRow.SubjectId));
            }

            if (authCodeRow.Acr != null)
            {
                claims.Add(new Claim("acr", authCodeRow.Acr));
            }

            if (authCodeRow.Nonce != null && isIDToken)
            {
                claims.Add(new Claim("nonce", authCodeRow.Nonce));
            }

            if (authCodeRow.AuthTime != null)
            {
                long authTimeEpoch = ((DateTimeOffset)authCodeRow.AuthTime).ToUnixTimeSeconds();
                claims.Add(new Claim("auth_time", authTimeEpoch.ToString(), ClaimValueTypes.Integer64));
            }

            if (!isIDToken && authCodeRow.Scopes != null && authCodeRow.Scopes.Any())
            {
                claims.Add(new Claim("scope", string.Join(" ", authCodeRow.Scopes)));
            }

            ClaimsIdentity identity = new(claims, "Token");
            ClaimsPrincipal principal = new(identity);

            return principal;
        }

        private async Task<string> GenerateToken(ClaimsPrincipal principal, DateTime? expires = null)
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            X509Certificate2 certificate = GetLatestCertificateWithRolloverDelay(
                certificates, _generalSettings.JwtSigningCertificateRolloverDelayHours);

            TimeSpan tokenExpiry = new TimeSpan(0, _generalSettings.JwtValidityMinutes, 0);
            if (expires == null)
            {
                expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds);
            }

            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = expires,
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
            var rolloverCutoff = DateTime.Now.AddHours(-rolloverDelayHours);
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
