using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Altinn.Platform.Authentication.Model;
using Microsoft.IdentityModel.Tokens;

#nullable enable

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Builds a DPoP proof (RFC 9449) for an outgoing request to an upstream provider.
    /// </summary>
    /// <remarks>
    /// A DPoP proof is a separate JWT from the client assertion and does not replace it: the
    /// assertion authenticates the client, the proof demonstrates possession of the key the issued
    /// tokens are bound to. Both are sent on the same token request.
    /// <para>
    /// Unlike the assertion, the proof carries the <em>public</em> key in its header, which is what
    /// lets the provider bind the token without knowing the key in advance.
    /// </para>
    /// </remarks>
    public static class DpopProofBuilder
    {
        /// <summary>
        /// The <c>typ</c> header required by RFC 9449, distinguishing a proof from any other JWT.
        /// </summary>
        private const string DpopTokenType = "dpop+jwt";

        /// <summary>
        /// Lifetime of a proof. It is bound to one request to one URL and is replayed nowhere, so
        /// this only needs to cover the request itself plus clock skew.
        /// </summary>
        private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Builds a proof for <paramref name="httpMethod"/> against <paramref name="requestUri"/>.
        /// </summary>
        /// <param name="provider">Provider whose client-assertion key signs the proof.</param>
        /// <param name="httpMethod">The HTTP method, used as the <c>htm</c> claim.</param>
        /// <param name="requestUri">The request URL. Query and fragment are stripped for <c>htu</c>.</param>
        /// <param name="now">Current time.</param>
        /// <param name="nonce">
        /// Server-supplied nonce, when the provider has answered a previous attempt with
        /// <c>use_dpop_nonce</c>. Omitted on a first attempt.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the provider has no usable signing key, for the same reason
        /// <see cref="ClientAssertionBuilder"/> does: a misconfiguration should fail with a cause,
        /// not as an opaque rejection from the provider.
        /// </exception>
        public static string Build(OidcProvider provider, string httpMethod, string requestUri, DateTimeOffset now, string? nonce = null)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
            ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);

            if (!ClientAssertionBuilder.IsConfiguredFor(provider))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has UseDpop set but no client assertion key configured; the proof is signed with that key.");
            }

            ClientAssertionBuilder.ImportedKey imported = ClientAssertionBuilder.ImportPrivateKey(provider);
            using RSA rsa = imported.Rsa;

            RsaSecurityKey key = new(rsa);
            string algorithm = ClientAssertionBuilder.ResolveAlgorithm(provider, imported);

            // The proof carries the public key so the provider can bind the token to it. Exporting
            // the public half explicitly keeps the private parameters out of the JWK by
            // construction rather than by trusting the serializer.
            RsaSecurityKey publicKey = new(rsa.ExportParameters(includePrivateParameters: false));
            JsonWebKey publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
            publicJwk.Alg = algorithm;

            SigningCredentials credentials = new(key, algorithm)
            {
                // The key lives only for this call, so a cached provider could never be reused.
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            };

            // Built explicitly rather than through SecurityTokenDescriptor: JwtSecurityTokenHandler
            // ignores AdditionalHeaderClaims, and the public key must reach the header — it is what
            // the provider binds the issued token to.
            JwtHeader header = new(credentials)
            {
                ["typ"] = DpopTokenType,
                ["jwk"] = BuildPublicJwkHeader(publicJwk),
            };

            JwtPayload payload = new()
            {
                // Unique per proof; the provider rejects a replay.
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                ["htm"] = httpMethod.ToUpperInvariant(),
                ["htu"] = NormaliseHtu(requestUri),
                [JwtRegisteredClaimNames.Iat] = now.ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Nbf] = now.ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Exp] = now.Add(Lifetime).ToUnixTimeSeconds(),
            };

            if (!string.IsNullOrWhiteSpace(nonce))
            {
                payload["nonce"] = nonce;
            }

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        }

        /// <summary>
        /// RFC 9449 requires <c>htu</c> to be the request URL without query or fragment.
        /// </summary>
        private static string NormaliseHtu(string requestUri)
        {
            if (!Uri.TryCreate(requestUri, UriKind.Absolute, out Uri? uri))
            {
                return requestUri;
            }

            return uri.GetLeftPart(UriPartial.Path);
        }

        /// <summary>
        /// The public JWK for the proof header, restricted to the RSA public members.
        /// </summary>
        private static Dictionary<string, object> BuildPublicJwkHeader(JsonWebKey publicJwk)
            => new()
            {
                ["kty"] = publicJwk.Kty,
                ["n"] = publicJwk.N,
                ["e"] = publicJwk.E,
                ["alg"] = publicJwk.Alg,
            };
    }
}
