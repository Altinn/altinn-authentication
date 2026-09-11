using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Tests.Fakes
{
    /// <inheritdoc />
    public class SigningKeysRetrieverStub : ISigningKeysRetriever
    {
        private const string WellKnownSuffix = "/.well-known/openid-configuration";

        /// <summary>
        /// Issuers to report for specific discovery URLs, overriding the default. Lets a test make a
        /// provider's discovery document disagree with its configuration.
        /// </summary>
        public ConcurrentDictionary<string, string?> IssuerOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When set, <see cref="GetIssuer"/> throws it, as an unreachable discovery endpoint would.
        /// </summary>
        public Exception? IssuerFailure { get; set; }

        /// <inheritdoc />
        public async Task<ICollection<SecurityKey>> GetSigningKeys(string url)
        {
            List<SecurityKey> signingKeys = new List<SecurityKey>();

            X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile("JWTValidationCert.cer");
            SecurityKey key = new X509SecurityKey(cert);

            signingKeys.Add(key);

            return await Task.FromResult(signingKeys);
        }

        /// <inheritdoc />
        /// <remarks>
        /// By default reports the issuer implied by the discovery URL — the URL with
        /// <c>/.well-known/openid-configuration</c> removed — which is the convention real providers
        /// follow and keeps a correctly configured test provider consistent without setup.
        /// </remarks>
        public Task<string?> GetIssuer(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IssuerFailure is not null)
            {
                throw IssuerFailure;
            }

            if (IssuerOverrides.TryGetValue(url, out string? overridden))
            {
                return Task.FromResult(overridden);
            }

            string issuer = url.EndsWith(WellKnownSuffix, StringComparison.OrdinalIgnoreCase)
                ? url[..^WellKnownSuffix.Length]
                : url;

            return Task.FromResult<string?>(issuer);
        }
    }
}
