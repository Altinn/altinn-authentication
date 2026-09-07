using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Altinn.Platform.Authentication.Model;
using Microsoft.IdentityModel.Tokens;

#nullable enable

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Builds a <c>private_key_jwt</c> client assertion (RFC 7523) for authenticating Altinn as an
    /// OIDC client against an upstream provider's token endpoint.
    /// </summary>
    /// <remarks>
    /// Needed because some providers do not accept a client secret at all. HelseID's security
    /// profile permits no other client authentication mechanism, so without this the token request
    /// is refused with <c>invalid_client</c> no matter how the provider is otherwise configured.
    /// </remarks>
    public static class ClientAssertionBuilder
    {
        /// <summary>
        /// The <c>client_assertion_type</c> value required by RFC 7523.
        /// </summary>
        public const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

        /// <summary>
        /// The <c>typ</c> header value. Not the JWT default: it marks the token as a client
        /// authentication assertion so it cannot be mistaken for, or replayed as, another kind of
        /// token. HelseID requires exactly this value.
        /// </summary>
        private const string ClientAuthenticationTokenType = "client-authentication+jwt";

        /// <summary>
        /// Lifetime of the assertion. Deliberately tiny — it is sent once, directly to the token
        /// endpoint, and never stored. HelseID rejects anything more than 10 seconds ahead.
        /// </summary>
        private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Builds a signed client assertion for <paramref name="provider"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the provider is not configured for client-assertion authentication, or the
        /// configured key or algorithm cannot be used. Thrown rather than returning null so a
        /// misconfiguration surfaces as a failed sign-in with a clear cause, instead of an
        /// <c>invalid_client</c> from the upstream provider that says nothing about why.
        /// </exception>
        public static string Build(OidcProvider provider, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (string.IsNullOrWhiteSpace(provider.ClientAssertionPrivateKeyPem))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has no ClientAssertionPrivateKeyPem configured.");
            }

            if (string.IsNullOrWhiteSpace(provider.ClientId))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has no ClientId configured; it is required as both iss and sub of the assertion.");
            }

            // The audience is the provider's issuer identifier, not its token endpoint. Several
            // providers accepted the endpoint URL historically; HelseID explicitly does not.
            string audience = string.IsNullOrWhiteSpace(provider.ClientAssertionAudience)
                ? provider.Issuer
                : provider.ClientAssertionAudience!;

            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has neither ClientAssertionAudience nor Issuer configured; one is required as the assertion audience.");
            }

            SigningCredentials credentials = CreateSigningCredentials(provider);

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = provider.ClientId,
                Audience = audience,
                TokenType = ClientAuthenticationTokenType,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.Add(Lifetime).UtcDateTime,
                SigningCredentials = credentials,
                Claims = new System.Collections.Generic.Dictionary<string, object>
                {
                    // 'sub' must equal the client id. The descriptor's Subject is a
                    // ClaimsIdentity, which would prefix claim types, so set it directly.
                    [JwtRegisteredClaimNames.Sub] = provider.ClientId,

                    // Single-use marker. The provider rejects a replayed jti.
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                },
            };

            JwtSecurityTokenHandler handler = new();
            handler.SetDefaultTimesOnTokenCreation = false;

            return handler.CreateEncodedJwt(descriptor);
        }

        private static SigningCredentials CreateSigningCredentials(OidcProvider provider)
        {
            RSA rsa = RSA.Create();
            try
            {
                // Accepts both PKCS#8 ("BEGIN PRIVATE KEY") and PKCS#1 ("BEGIN RSA PRIVATE KEY").
                rsa.ImportFromPem(provider.ClientAssertionPrivateKeyPem);
            }
            catch (ArgumentException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyPem for provider '{provider.IssuerKey}' is not a readable PEM private key.", ex);
            }

            RsaSecurityKey key = new(rsa) { KeyId = provider.ClientAssertionKeyId };

            string algorithm = string.IsNullOrWhiteSpace(provider.ClientAssertionAlgorithm)
                ? SecurityAlgorithms.RsaSsaPssSha256
                : provider.ClientAssertionAlgorithm!;

            // Only asymmetric algorithms are meaningful here, and providers that mandate
            // private_key_jwt generally mandate PSS as well. Reject anything else outright rather
            // than let a typo produce a signature the provider silently refuses.
            if (algorithm is not (SecurityAlgorithms.RsaSsaPssSha256
                or SecurityAlgorithms.RsaSsaPssSha384
                or SecurityAlgorithms.RsaSsaPssSha512
                or SecurityAlgorithms.RsaSha256
                or SecurityAlgorithms.RsaSha384
                or SecurityAlgorithms.RsaSha512))
            {
                rsa.Dispose();
                throw new InvalidOperationException(
                    $"ClientAssertionAlgorithm '{algorithm}' for provider '{provider.IssuerKey}' is not a supported asymmetric RSA algorithm.");
            }

            return new SigningCredentials(key, algorithm);
        }
    }
}
