using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
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
        public async Task<JwtSecurityToken> ValidateTokenAsync(string token, OidcProvider provider, string audience, CancellationToken ct = default)
        {
            ICollection<SecurityKey> signingKeys = await _signingKeysRetriever.GetSigningKeys(provider.WellKnownConfigEndpoint);
            return ValidateToken(token, signingKeys);
        }

        private JwtSecurityToken ValidateToken(string originalToken, ICollection<SecurityKey> signingKeys)
        {
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            _validator.ValidateToken(originalToken, validationParameters, out _);
            JwtSecurityToken token = _validator.ReadJwtToken(originalToken);
            return token;
        }
    }
}
