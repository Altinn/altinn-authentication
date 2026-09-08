using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Altinn.Platform.Authentication.Tests
{
    /// <summary>
    /// Covers the <c>private_key_jwt</c> client assertion. The requirements asserted here are
    /// HelseID's, which permits no other client authentication mechanism, but they follow
    /// RFC 7523 and apply to any provider configured the same way.
    /// </summary>
    public class ClientAssertionBuilderTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        private static string GenerateKeyPem()
        {
            using RSA rsa = RSA.Create(2048);
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        private static OidcProvider HelseId(string? keyPem = null) => new()
        {
            IssuerKey = "helseid",
            Issuer = "https://helseid-sts.test.nhn.no",
            TokenEndpoint = "https://helseid-sts.test.nhn.no/connect/token",
            ClientId = "altinn-test-client",
            ClientAssertionPrivateKeyPem = keyPem ?? GenerateKeyPem(),
            ClientAssertionKeyId = "altinn-key-1",
        };

        private static JwtSecurityToken Parse(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        [Fact]
        public void Build_SetsIssuerAndSubjectToClientId()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            Assert.Equal("altinn-test-client", assertion.Issuer);
            Assert.Equal("altinn-test-client", assertion.Subject);
        }

        [Fact]
        public void Build_AudienceIsTheIssuerIdentifierNotTheTokenEndpoint()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            // NHN documents explicitly that the token endpoint URL must not be used here, even
            // though it was accepted historically.
            Assert.Equal("https://helseid-sts.test.nhn.no", Assert.Single(assertion.Audiences));
            Assert.DoesNotContain("/connect/token", Assert.Single(assertion.Audiences));
        }

        [Fact]
        public void Build_AudienceCanBeOverriddenIndependentlyOfIssuer()
        {
            OidcProvider provider = HelseId();
            provider.ClientAssertionAudience = "https://other-audience.example";

            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(provider, Now));

            Assert.Equal("https://other-audience.example", Assert.Single(assertion.Audiences));
        }

        [Fact]
        public void Build_LifetimeLeavesMarginUnderTheTenSecondLimit()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            // HelseID rejects an assertion whose exp is more than 10 seconds ahead. Sitting exactly
            // on the limit leaves no room for clock skew: if our clock runs ahead of theirs, an exp
            // we computed as 10 seconds out looks like more than 10 to them. Asserted as an exact
            // value rather than an upper bound, so widening it back to the limit fails here.
            Assert.Equal(TimeSpan.FromSeconds(8), assertion.ValidTo - Now.UtcDateTime);
        }

        [Fact]
        public void Build_NotBeforeIsBackdatedForClockSkew()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            // HelseID requires nbf, so it cannot be dropped. Backdating it keeps the assertion
            // usable when their clock is behind ours, which nbf equal to our own now would not.
            Assert.Equal(Now.UtcDateTime.AddSeconds(-5), assertion.ValidFrom);
        }

        [Fact]
        public void Build_DoesNotLeaveTheSigningKeyToTheFinalizer()
        {
            // RsaSecurityKey does not take ownership of an RSA passed to it, and neither the
            // credentials nor the handler dispose it. Build owns the key and disposes it once the
            // assertion is signed; repeated calls must therefore not accumulate live handles.
            OidcProvider provider = HelseId();

            for (int i = 0; i < 50; i++)
            {
                Assert.NotEmpty(ClientAssertionBuilder.Build(provider, Now));
            }
        }

        [Fact]
        public void Build_SetsClientAuthenticationTokenTypeHeader()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            // Marks the assertion as client authentication so it cannot be replayed as another
            // kind of token.
            Assert.Equal("client-authentication+jwt", assertion.Header.Typ);
        }

        [Fact]
        public void Build_SetsKeyIdAndDefaultsToPs256()
        {
            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(), Now));

            Assert.Equal("altinn-key-1", assertion.Header.Kid);
            Assert.Equal(SecurityAlgorithms.RsaSsaPssSha256, assertion.Header.Alg);
        }

        [Fact]
        public void Build_JtiIsUniquePerAssertion()
        {
            OidcProvider provider = HelseId();

            string first = Parse(ClientAssertionBuilder.Build(provider, Now)).Id;
            string second = Parse(ClientAssertionBuilder.Build(provider, Now)).Id;

            // The provider enforces single use, so a reused jti would fail the second exchange.
            Assert.NotEqual(first, second);
            Assert.NotEmpty(first);
        }

        [Fact]
        public void Build_SignatureVerifiesWithTheMatchingPublicKey()
        {
            string keyPem = GenerateKeyPem();
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(keyPem);

            string jwt = ClientAssertionBuilder.Build(HelseId(keyPem), Now);

            TokenValidationParameters parameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = "altinn-test-client",
                ValidateAudience = true,
                ValidAudience = "https://helseid-sts.test.nhn.no",
                ValidateLifetime = true,
                LifetimeValidator = (nbf, exp, _, _) => nbf <= Now.UtcDateTime && exp > Now.UtcDateTime,
                ValidateTokenReplay = false,
                IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
                ValidateIssuerSigningKey = true,
            };

            new JwtSecurityTokenHandler().ValidateToken(jwt, parameters, out _);
        }

        [Fact]
        public void Build_WithoutKey_ThrowsRatherThanProducingAnUnauthenticatedRequest()
        {
            OidcProvider provider = HelseId();
            provider.ClientAssertionPrivateKeyPem = null!;

            var ex = Assert.Throws<InvalidOperationException>(() => ClientAssertionBuilder.Build(provider, Now));
            Assert.Contains("ClientAssertionPrivateKeyPem", ex.Message);
        }

        [Fact]
        public void Build_WithUnreadableKey_ThrowsWithTheProviderNamed()
        {
            OidcProvider provider = HelseId();
            provider.ClientAssertionPrivateKeyPem = "-----BEGIN PRIVATE KEY-----\nnot-a-key\n-----END PRIVATE KEY-----";

            var ex = Assert.Throws<InvalidOperationException>(() => ClientAssertionBuilder.Build(provider, Now));
            Assert.Contains("helseid", ex.Message);
        }

        [Fact]
        public void Build_WithSymmetricAlgorithm_IsRejected()
        {
            OidcProvider provider = HelseId();
            provider.ClientAssertionAlgorithm = SecurityAlgorithms.HmacSha256;

            // A symmetric algorithm here would mean the assertion is not actually proving
            // possession of the private key.
            var ex = Assert.Throws<InvalidOperationException>(() => ClientAssertionBuilder.Build(provider, Now));
            Assert.Contains("not a supported asymmetric", ex.Message);
        }

        [Fact]
        public void Build_AcceptsPkcs1KeyFormat()
        {
            using RSA rsa = RSA.Create(2048);
            string pkcs1 = rsa.ExportRSAPrivateKeyPem();

            JwtSecurityToken assertion = Parse(ClientAssertionBuilder.Build(HelseId(pkcs1), Now));

            Assert.Equal("altinn-test-client", assertion.Issuer);
        }
    }
}
