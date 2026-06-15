using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Mockporten.Configuration;
using Mockporten.Models;
using Mockporten.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mockporten.Controllers
{
    /// <summary>
    /// Represents a controller that can expose standard endpoints published by an open id provider.
    /// </summary>
    [Route("/api/v1/openid/.well-known")]
    [AllowAnonymous]
    [ApiController]
    public class OpenIdController : ControllerBase
    {
        private readonly GeneralSettings _generalSettings;

        private readonly IJwtSigningCertificateProvider _certificateProvider;

        /// <summary>
        /// Initialise a new instance of <see cref="OpenIdController"/> with the given input values.
        /// </summary>
        /// <param name="generalSettings">The application general settings.</param>
        /// <param name="certificateProvider">A service able to obtain a list of valid certificates that can be used to sign/validate a JWT.</param>
        public OpenIdController(
            IOptions<GeneralSettings> generalSettings,
            IJwtSigningCertificateProvider certificateProvider)
        {
            _generalSettings = generalSettings.Value;
            _certificateProvider = certificateProvider;
        }

        /// <summary>
        /// Returns a discovery document
        /// </summary>
        /// <returns>The configuration object for Open ID Connect.</returns>
        [HttpGet("openid-configuration")]
        [Produces("application/json")]
        public async Task<IActionResult> GetOpenIdConfigurationAsync()
        {
            string root = _generalSettings.IdProviderEndpoint.TrimEnd('/') + "/";

            OpenIdConnectConfiguration discoveryDocument = new OpenIdConnectConfiguration
            {
                // REQUIRED
                Issuer = root,

                // REQUIRED - the login (authorization) endpoint (AuthorizeController).
                AuthorizationEndpoint = root + "Authorize",

                // REQUIRED unless only the Implicit Flow is used - the token endpoint (TokenController).
                TokenEndpoint = root + "token",

                // REQUIRED - this controller's JWKS endpoint.
                JwksUri = root + "api/v1/openid/.well-known/openid-configuration/jwks",

                // REQUIRED - this provider implements the authorization-code flow.
                ResponseTypesSupported = { "code" },

                // REQUIRED
                SubjectTypesSupported = { "pairwise" },

                // REQUIRED
                IdTokenSigningAlgValuesSupported = {"RS256"},

                FrontchannelLogoutSessionSupported = "false",

                FrontchannelLogoutSupported = "false"

            };

            return await Task.FromResult(Ok(discoveryDocument));
        }

        /// <summary>
        /// Returns the JSON Web Key Set to use when validating a token.
        /// </summary>
        /// <returns>The Altinn JSON Web Key Set.</returns>
        [HttpGet("openid-configuration/jwks")]
        public async Task<IActionResult> GetJsonWebKeySetAsync()
        {
            JwksDocument jwksDocument = new JwksDocument
            {
                Keys = new List<JwkDocument>()
            };

            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            foreach (X509Certificate2 cert in certificates)
            {
                RSA rsaPublicKey = cert.GetRSAPublicKey();
                if (rsaPublicKey is null)
                {
                    throw new InvalidOperationException($"Certificate {cert.Thumbprint} does not contain an RSA public key.");
                }

                RSAParameters exportParameters = rsaPublicKey.ExportParameters(false);

                // RFC 7518: JWK 'n' (modulus) and 'e' (exponent) are base64url-encoded.
                string exponent = Base64UrlEncoder.Encode(exportParameters.Exponent);
                string modulus = Base64UrlEncoder.Encode(exportParameters.Modulus);

                List<string> chain = ExportChain(cert);

                JwkDocument jwkDocument = new JwkDocument
                {
                    KeyType = "RSA", PublicKeyUse = "sig", KeyId = cert.Thumbprint, Exponent = exponent, Modulus = modulus, X509Chain = chain
                };

                jwksDocument.Keys.Add(jwkDocument);
            }

            return Ok(jwksDocument);
        }

        private List<string> ExportChain(X509Certificate2 cert)
        {
            List<string> result = new List<string>();

            using (X509Chain chain = new X509Chain { ChainPolicy = { RevocationMode = X509RevocationMode.NoCheck } })
            {
                chain.Build(cert);

                foreach (X509ChainElement chainElement in chain.ChainElements)
                {
                    string export = Convert.ToBase64String(chainElement.Certificate.Export(X509ContentType.Cert));
                    result.Add(export);
                }
            }

            return result;
        }
    }
}
