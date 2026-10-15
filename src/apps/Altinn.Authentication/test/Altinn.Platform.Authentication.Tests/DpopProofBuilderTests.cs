using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Altinn.Platform.Authentication.Tests
{
    /// <summary>
    /// Covers the DPoP proof (RFC 9449) sent with the token request. HelseID requires it for every
    /// grant type, including <c>authorization_code</c>.
    /// </summary>
    public class DpopProofBuilderTests
    {
        private const string TokenEndpoint = "https://helseid-sts.test.nhn.no/connect/token";

        private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

        private static OidcProvider HelseId()
        {
            using RSA rsa = RSA.Create(2048);
            return new OidcProvider
            {
                IssuerKey = "helseid",
                Issuer = "https://helseid-sts.test.nhn.no",
                TokenEndpoint = TokenEndpoint,
                ClientId = "altinn-test-client",
                ClientAssertionPrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
                UseDpop = true,
            };
        }

        private static JwtSecurityToken Parse(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        [Fact]
        public void Build_SetsDpopTokenTypeHeader()
        {
            JwtSecurityToken proof = Parse(DpopProofBuilder.Build(HelseId(), "POST", TokenEndpoint, Now));

            // Distinguishes a proof from any other JWT, so it cannot be replayed as one.
            Assert.Equal("dpop+jwt", proof.Header.Typ);
        }

        [Fact]
        public void Build_BindsToMethodAndUrl()
        {
            JwtSecurityToken proof = Parse(DpopProofBuilder.Build(HelseId(), "post", TokenEndpoint, Now));

            Assert.Equal("POST", proof.Claims.First(c => c.Type == "htm").Value);
            Assert.Equal(TokenEndpoint, proof.Claims.First(c => c.Type == "htu").Value);
        }

        [Fact]
        public void Build_HtuExcludesQueryAndFragment()
        {
            // RFC 9449 requires htu to be the URL without query or fragment.
            JwtSecurityToken proof = Parse(
                DpopProofBuilder.Build(HelseId(), "POST", TokenEndpoint + "?a=1#frag", Now));

            Assert.Equal(TokenEndpoint, proof.Claims.First(c => c.Type == "htu").Value);
        }

        [Fact]
        public void Build_CarriesThePublicKeyAndNeverThePrivateHalf()
        {
            string encoded = DpopProofBuilder.Build(HelseId(), "POST", TokenEndpoint, Now);
            JwtSecurityToken proof = Parse(encoded);

            Assert.True(proof.Header.TryGetValue("jwk", out object? jwkHeader));
            string jwkJson = JsonSerializer.Serialize(jwkHeader);

            // The provider binds the token to this key, so the public half must be present.
            Assert.Contains("\"kty\"", jwkJson);
            Assert.Contains("\"n\"", jwkJson);
            Assert.Contains("\"e\"", jwkJson);

            // And the private components must not be, under any name.
            foreach (string secret in new[] { "\"d\"", "\"p\"", "\"q\"", "\"dp\"", "\"dq\"", "\"qi\"" })
            {
                Assert.DoesNotContain(secret, jwkJson);
            }
        }

        [Fact]
        public void Build_WithoutNonce_OmitsTheClaim()
        {
            JwtSecurityToken proof = Parse(DpopProofBuilder.Build(HelseId(), "POST", TokenEndpoint, Now));

            Assert.DoesNotContain(proof.Claims, c => c.Type == "nonce");
        }

        [Fact]
        public void Build_WithNonce_IncludesIt()
        {
            // Supplied only after the provider rejects a first attempt with use_dpop_nonce.
            JwtSecurityToken proof = Parse(
                DpopProofBuilder.Build(HelseId(), "POST", TokenEndpoint, Now, nonce: "server-nonce"));

            Assert.Equal("server-nonce", proof.Claims.First(c => c.Type == "nonce").Value);
        }

        [Fact]
        public void Build_JtiIsUniquePerProof()
        {
            OidcProvider provider = HelseId();

            string first = Parse(DpopProofBuilder.Build(provider, "POST", TokenEndpoint, Now)).Id;
            string second = Parse(DpopProofBuilder.Build(provider, "POST", TokenEndpoint, Now)).Id;

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Build_SignatureVerifiesWithTheKeyInItsOwnHeader()
        {
            OidcProvider provider = HelseId();
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(provider.ClientAssertionPrivateKeyPem);

            string encoded = DpopProofBuilder.Build(provider, "POST", TokenEndpoint, Now);

            new JwtSecurityTokenHandler().ValidateToken(
                encoded,
                new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    LifetimeValidator = (nbf, exp, _, _) => nbf <= Now.UtcDateTime && exp > Now.UtcDateTime,
                    IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
                },
                out _);
        }

        [Fact]
        public void Build_WithoutAssertionKey_ThrowsNamingTheProvider()
        {
            OidcProvider provider = HelseId();
            provider.ClientAssertionPrivateKeyPem = null!;

            // The proof is signed with the client assertion key; without one there is nothing to
            // demonstrate possession of.
            var ex = Assert.Throws<InvalidOperationException>(
                () => DpopProofBuilder.Build(provider, "POST", TokenEndpoint, Now));

            Assert.Contains("helseid", ex.Message);
        }
    }
}
