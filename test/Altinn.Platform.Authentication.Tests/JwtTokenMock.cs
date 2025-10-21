using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Altinn.Common.AccessToken.Constants;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Tests
{
    /// <summary>
    /// Represents a mechanism for creating JSON Web tokens for use in integration tests.
    /// </summary>
    public static class JwtTokenMock
    {
        /// <summary>
        /// Generates a token with a self signed certificate included in the integration test project.
        /// </summary>
        /// <param name="principal">The claims principal to include in the token.</param>
        /// <param name="tokenExpiry">How long the token should be valid for.</param>
        /// <returns>A new token.</returns>
        public static string GenerateToken(ClaimsPrincipal principal, TimeSpan tokenExpiry)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds),
                SigningCredentials = GetSigningCredentials(),
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            string serializedToken = tokenHandler.WriteToken(token);

            return serializedToken;
        }

        /// <summary>
        /// Creates a encrypted token
        /// </summary>
        /// <param name="principal">The claims principal to include in the token.</param>
        /// <param name="tokenExpiry">How long the token should be valid for.</param>
        /// <returns>A new token.</returns>
        public static string GenerateEncryptedToken(ClaimsPrincipal principal, TimeSpan tokenExpiry)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds),
                EncryptingCredentials = GetEncryptionCredentials(),
            };

            string token = tokenHandler.CreateEncodedJwt(tokenDescriptor);
            return token;
        }

        /// <summary>
        /// Creates a encrypted and signed token
        /// </summary>
        /// <param name="principal">The claims principal to include in the token.</param>
        /// <param name="tokenExpiry">How long the token should be valid for.</param>
        /// <returns>A new token.</returns>
        public static string GenerateEncryptedAndSignedToken(ClaimsPrincipal principal, TimeSpan tokenExpiry)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds),
                EncryptingCredentials = GetEncryptionCredentials(),
                SigningCredentials = GetSigningCredentials(),
            };

            string token = tokenHandler.CreateEncodedJwt(tokenDescriptor);
            return token;
        }

        /// <summary>
        /// Creates an access token based on the issuer.
        /// </summary>
        /// <param name="issuer">The issuer of the token. E.g. studio</param>
        /// <param name="app">The application the token is generated for</param>
        /// <param name="tokenExpiry">How long the token should be valid for.</param>
        /// <returns>A new token</returns>
        public static string GenerateAccessToken(string issuer, string app, TimeSpan tokenExpiry)
        {
            List<Claim> claims = [new Claim(AccessTokenClaimTypes.App, app, ClaimValueTypes.String, issuer)];

            ClaimsIdentity identity = new("AccessToken");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);

            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = DateTime.Now.Add(tokenExpiry),
                SigningCredentials = GetAccesTokenCredentials(issuer),
                Audience = "platform.altinn.no",
                Issuer = issuer
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            string tokenstring = tokenHandler.WriteToken(token);

            return tokenstring;
        }

        private static SigningCredentials GetAccesTokenCredentials(string issuer)
        {
            string certPath = $"{issuer}-org.pfx";

            X509Certificate2 certIssuer = X509CertificateLoader.LoadPkcs12FromFile(certPath, password: default);
            return new X509SigningCredentials(certIssuer, SecurityAlgorithms.RsaSha256);
        }

        /// <summary>
        /// Validates a token and return the ClaimsPrincipal if successful. The validation key used is from the self signed certificate
        /// and is included in the integration test project as a separate file.
        /// </summary>
        /// <param name="token">The token to be validated.</param>
        /// <param name="now">Current time if needing to override</param>
        /// <returns>ClaimsPrincipal</returns>
        public static ClaimsPrincipal ValidateToken(string token, DateTimeOffset? now = null)
        {
            if (now == null)
            {
                now = DateTimeOffset.UtcNow;
            }

            X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile("selfSignedTestCertificatePublic.cer");
            SecurityKey key = new X509SecurityKey(cert);

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10),
                LifetimeValidator = GetLifetimeValidator(now.Value)
            };

            JwtSecurityTokenHandler validator = new()
            {
                MapInboundClaims = false
            };
            return validator.ValidateToken(token, validationParameters, out _);
        }

        /// <summary>
        /// Converts to security token
        /// </summary>
        public static SecurityToken GetSecurityToken(string token)
        {
            X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile("selfSignedTestCertificatePublic.cer");
            SecurityKey key = new X509SecurityKey(cert);

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            JwtSecurityTokenHandler validator = new();

            SecurityToken securityToken;
            validator.ValidateToken(token, validationParameters, out securityToken);
            return securityToken;
        }

        /// <summary>
        /// Validates a token and return the ClaimsPrincipal if successful. The validation key used is from the self signed certificate
        /// and is included in the integration test project as a separate file.
        /// </summary>
        /// <param name="token">The token to be validated.</param>
        /// <returns>ClaimsPrincipal</returns>
        public static ClaimsPrincipal ValidateEncryptedAndSignedToken(string token)
        {
            X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile("selfSignedTestCertificatePublic.cer");
            SecurityKey key = new X509SecurityKey(cert);

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                TokenDecryptionKey = new X509SecurityKey(X509CertificateLoader.LoadPkcs12FromFile("selfSignedEncryptionTestCertificate.pfx", "qwer1234", X509KeyStorageFlags.EphemeralKeySet)),
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            JwtSecurityTokenHandler validator = new();
            return validator.ValidateToken(token, validationParameters, out _);
        }

        private static SigningCredentials GetSigningCredentials()
        {
            X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile("selfSignedTestCertificate.pfx", "qwer1234", X509KeyStorageFlags.EphemeralKeySet);
            return new X509SigningCredentials(cert, SecurityAlgorithms.RsaSha256);
        }

        private static EncryptingCredentials GetEncryptionCredentials()
        {
            return new X509EncryptingCredentials(X509CertificateLoader.LoadCertificateFromFile("selfSignedEncryptionTestCertificatePublic.cer"));
        }

        private static LifetimeValidator GetLifetimeValidator(DateTimeOffset now)
        {
            // Use our own "now" for lifetime checks
            LifetimeValidator lifetimeValidator = (nbf, exp, _token, p) =>
            {
                var n = now.UtcDateTime;
                if (nbf.HasValue && n < nbf.Value - p.ClockSkew)
                {
                    return false;
                }

                if (exp.HasValue && n > exp.Value + p.ClockSkew)
                {
                    return false;
                }

                return true;
            };

            return lifetimeValidator;
        }
    }
}
