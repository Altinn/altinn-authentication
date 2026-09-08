using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Altinn.Platform.Authentication.Model;
using Microsoft.IdentityModel.Tokens;
using JwtBase64Url = Microsoft.IdentityModel.Tokens.Base64UrlEncoder;

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
        /// endpoint, and never stored.
        /// </summary>
        /// <remarks>
        /// Two seconds under HelseID's limit of 10. Sitting exactly on the limit leaves no room for
        /// clock skew: if our clock runs ahead of theirs, an <c>exp</c> we computed as 10 seconds
        /// out looks like more than 10 to them, and the assertion is refused.
        /// </remarks>
        private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);

        /// <summary>
        /// How far <c>nbf</c> is backdated. HelseID requires the claim, so it cannot simply be
        /// omitted, and <c>nbf</c> equal to our own clock is refused whenever theirs is behind
        /// ours. Backdating absorbs that without extending the window meaningfully — the assertion
        /// is still only valid for a few seconds around now.
        /// </remarks>
        private static readonly TimeSpan NotBeforeSkew = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Whether <paramref name="provider"/> is configured to authenticate with a client
        /// assertion rather than a client secret.
        /// </summary>
        /// <remarks>
        /// The single place that decides this. Callers must not re-derive it from the individual
        /// key settings: a caller that checked only one of them would leave the other format
        /// silently inert, sending a client secret — or nothing — where an assertion was intended.
        /// </remarks>
        public static bool IsConfiguredFor(OidcProvider provider)
            => provider is not null
                && (!string.IsNullOrWhiteSpace(provider.ClientAssertionPrivateKeyPem)
                    || !string.IsNullOrWhiteSpace(provider.ClientAssertionPrivateKeyJwk));

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

            if (!IsConfiguredFor(provider))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has neither ClientAssertionPrivateKeyPem nor ClientAssertionPrivateKeyJwk configured.");
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

            // The key is owned here and disposed once the assertion is signed. RsaSecurityKey does
            // not take ownership of an RSA handed to it, and neither the credentials nor the
            // handler dispose it, so without this every sign-in would leave a native key handle to
            // the finalizer.
            ImportedKey imported = ImportPrivateKey(provider);
            using RSA rsa = imported.Rsa;

            SigningCredentials credentials = CreateSigningCredentials(provider, imported);

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = provider.ClientId,
                Audience = audience,
                TokenType = ClientAuthenticationTokenType,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.Subtract(NotBeforeSkew).UtcDateTime,
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

        /// <summary>
        /// The imported key, together with the <c>kid</c> and <c>alg</c> the key material itself
        /// declared. A JWK carries both; a PEM carries neither.
        /// </summary>
        private sealed record ImportedKey(RSA Rsa, string? KeyId, string? Algorithm);

        private static ImportedKey ImportPrivateKey(OidcProvider provider)
        {
            bool hasPem = !string.IsNullOrWhiteSpace(provider.ClientAssertionPrivateKeyPem);
            bool hasJwk = !string.IsNullOrWhiteSpace(provider.ClientAssertionPrivateKeyJwk);

            // Ambiguous configuration, with no sensible precedence to pick. Say so rather than
            // silently signing with one of them.
            if (hasPem && hasJwk)
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.IssuerKey}' has both ClientAssertionPrivateKeyPem and ClientAssertionPrivateKeyJwk configured. Set exactly one.");
            }

            return hasJwk ? ImportFromJwk(provider) : new ImportedKey(ImportFromPem(provider), null, null);
        }

        private static RSA ImportFromPem(OidcProvider provider)
        {
            RSA rsa = RSA.Create();
            try
            {
                // Accepts both PKCS#8 ("BEGIN PRIVATE KEY") and PKCS#1 ("BEGIN RSA PRIVATE KEY").
                rsa.ImportFromPem(provider.ClientAssertionPrivateKeyPem);
                return rsa;
            }
            catch (ArgumentException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyPem for provider '{provider.IssuerKey}' is not a readable PEM private key.", ex);
            }
        }

        /// <summary>
        /// Imports a private RSA JWK, which is the format providers such as HelseID hand out at
        /// client registration.
        /// </summary>
        /// <remarks>
        /// Accepts the JWK verbatim or base64-encoded. The distinction is unambiguous — base64 of
        /// a JWK never begins with '{' — and having both means the value can be pasted as received
        /// where that is convenient, or encoded where raw JSON is awkward. A bare JSON object in a
        /// YAML <c>value:</c> is read as a flow mapping rather than a string unless quoted, which
        /// is a trap that does not fail loudly.
        /// </remarks>
        private static ImportedKey ImportFromJwk(OidcProvider provider)
        {
            string raw = provider.ClientAssertionPrivateKeyJwk.Trim();
            string json = raw.StartsWith('{') ? raw : DecodeBase64(raw, provider);

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' is not valid JSON.", ex);
            }

            // Everything taken out below is copied — strings and byte arrays — so the document is
            // only needed for the duration of this method.
            using (document)
            {
                return ReadJwk(document.RootElement, provider);
            }
        }

        private static ImportedKey ReadJwk(JsonElement jwk, OidcProvider provider)
        {
            // Parse accepts a bare scalar too, and TryGetProperty throws on anything but an object.
            if (jwk.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' is not a JSON object.");
            }

            string? keyType = ReadString(jwk, "kty");
            if (!string.Equals(keyType, "RSA", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' has kty '{keyType}'; only RSA is supported.");
            }

            // 'd' alone is a valid private key in JWK terms, but .NET's RSA implementations want
            // the CRT parameters as well, so the full set is required. ReadComponent checks
            // presence, type and encoding together, so every way a component can be wrong produces
            // a message naming the provider and the field rather than a raw framework exception.
            RSAParameters parameters = new()
            {
                Modulus = ReadComponent(jwk, "n", provider),
                Exponent = ReadComponent(jwk, "e", provider),
                D = ReadComponent(jwk, "d", provider),
                P = ReadComponent(jwk, "p", provider),
                Q = ReadComponent(jwk, "q", provider),
                DP = ReadComponent(jwk, "dp", provider),
                DQ = ReadComponent(jwk, "dq", provider),
                InverseQ = ReadComponent(jwk, "qi", provider),
            };

            RSA rsa = RSA.Create();
            try
            {
                rsa.ImportParameters(parameters);
            }
            catch (CryptographicException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' does not contain a usable RSA private key.", ex);
            }

            // The JWK states its own kid and alg. Taking them from here removes two settings that
            // would otherwise duplicate the key material and could drift out of step with it.
            return new ImportedKey(rsa, ReadString(jwk, "kid"), ReadString(jwk, "alg"));
        }

        private static string DecodeBase64(string value, OidcProvider provider)
        {
            try
            {
                // Base64UrlEncoder also passes '+' and '/' through unchanged, so this accepts
                // standard base64 as well as base64url — a key may have been encoded either way.
                return JwtBase64Url.Decode(value);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or DecoderFallbackException)
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' is neither a JSON object nor valid base64 of one.", ex);
            }
        }

        private static string? ReadString(JsonElement element, string name)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>
        /// Reads one base64url-encoded RSA component, failing with a message that names the
        /// provider and the field whether it is absent, not a string, or not decodable.
        /// </summary>
        private static byte[] ReadComponent(JsonElement jwk, string name, OidcProvider provider)
        {
            if (!jwk.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' is missing '{name}', or its value is not a string. A complete private RSA JWK is required; a public-only JWK cannot sign.");
            }

            try
            {
                return JwtBase64Url.DecodeBytes(element.GetString());
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new InvalidOperationException(
                    $"ClientAssertionPrivateKeyJwk for provider '{provider.IssuerKey}' has a '{name}' value that is not valid base64url.", ex);
            }
        }

        private static string? FirstNonEmpty(string? configured, string? fromKey)
            => !string.IsNullOrWhiteSpace(configured) ? configured
                : !string.IsNullOrWhiteSpace(fromKey) ? fromKey
                : null;

        private static SigningCredentials CreateSigningCredentials(OidcProvider provider, ImportedKey imported)
        {
            // Explicit configuration wins, then whatever the key material itself declared, then the
            // default. A JWK states its own alg, so configuring it separately is redundant and only
            // creates something that can drift out of step with the key.
            string algorithm =
                FirstNonEmpty(provider.ClientAssertionAlgorithm, imported.Algorithm) ?? SecurityAlgorithms.RsaSsaPssSha256;

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
                throw new InvalidOperationException(
                    $"ClientAssertionAlgorithm '{algorithm}' for provider '{provider.IssuerKey}' is not a supported asymmetric RSA algorithm.");
            }

            RsaSecurityKey key = new(imported.Rsa) { KeyId = FirstNonEmpty(provider.ClientAssertionKeyId, imported.KeyId) };

            return new SigningCredentials(key, algorithm)
            {
                // The key instance lives only for this call, so a cached signature provider could
                // never be reused — it would only accumulate entries wrapping disposed keys.
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            };
        }
    }
}
