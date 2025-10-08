using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services;

/// <summary>
/// Token service
/// </summary>
/// <param name="settings">Settings</param>
/// <param name="certificateProvider">Certificate provider</param>
public class TokenService(IOptions<GeneralSettings> settings, IJwtSigningCertificateProvider certificateProvider) : ITokenService
{
    private readonly GeneralSettings _settings = settings.Value;

    /// <summary>
    /// Generates a token and serialize it to a compact format
    /// </summary>
    /// <param name="principal">The claims principal for the token</param>
    /// <param name="expires">The Expiry time of the token</param>
    /// <returns>A serialized version of the generated JSON Web Token.</returns>
    public async Task<string> GenerateToken(ClaimsPrincipal principal, DateTime? expires = null)
    {
        List<X509Certificate2> certificates = await certificateProvider.GetCertificates();

        X509Certificate2 certificate = GetLatestCertificateWithRolloverDelay(
            certificates, _settings.JwtSigningCertificateRolloverDelayHours);

        TimeSpan tokenExpiry = new TimeSpan(0, _settings.JwtValidityMinutes, 0);
        if (expires == null)
        {
            expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds);
        }

        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
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
