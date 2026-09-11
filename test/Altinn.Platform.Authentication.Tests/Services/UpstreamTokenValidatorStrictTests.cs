#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    /// <summary>
    /// Covers <see cref="OidcProvider.StrictIdTokenValidation"/>: exact issuer and audience, applied
    /// to id_tokens only.
    /// </summary>
    /// <remarks>
    /// The token kind is passed explicitly. It used to be inferred from whether a nonce was
    /// supplied, which misclassified an id_token presented as <c>id_token_hint</c>, and applied the
    /// exact-issuer half of the rule to access tokens as well.
    /// </remarks>
    public sealed class UpstreamTokenValidatorStrictTests : IDisposable
    {
        private const string Issuer = "https://helseid-par.test.nhn.no";
        private const string ClientId = "altinn-par-client";

        private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

        public void Dispose() => _services.Dispose();

        private UpstreamTokenValidator CreateSut() => new(
            NullLogger<UpstreamTokenValidator>.Instance,
            new SigningKeysRetrieverStub(),
            new TestMetricsProvider(_services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()));

        private static OidcProvider Provider(bool strict) => new()
        {
            IssuerKey = "helseid-par",
            Issuer = Issuer,
            ClientId = ClientId,
            WellKnownConfigEndpoint = Issuer + "/.well-known/openid-configuration",
            StrictIdTokenValidation = strict,
        };

        private static string Token(string issuer, string audience)
        {
            ClaimsIdentity identity = new("mock");
            identity.AddClaims(
            [
                new Claim("iss", issuer),
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("aud", audience),
            ]);

            return JwtTokenMock.GenerateToken(new ClaimsPrincipal(identity), TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task Strict_IdTokenWithOurClientIdAsAudience_IsAccepted()
        {
            JwtSecurityToken token = await CreateSut().ValidateTokenAsync(
                Token(Issuer, ClientId), Provider(strict: true), UpstreamTokenKind.IdToken, nonce: null);

            Assert.Contains(ClientId, token.Audiences);
        }

        [Fact]
        public async Task Strict_IdTokenIssuedToAnotherClient_IsRejected()
        {
            // An id_token minted for someone else must not be presentable here.
            await Assert.ThrowsAnyAsync<SecurityTokenInvalidAudienceException>(() => CreateSut().ValidateTokenAsync(
                Token(Issuer, "some-other-client"), Provider(strict: true), UpstreamTokenKind.IdToken, nonce: null));
        }

        [Fact]
        public async Task Strict_AccessTokenAudiencedToTheApi_IsAccepted()
        {
            // The combination the settings allow: strict id_token validation, access token not
            // treated as opaque. An access token is audienced to the API, so requiring our client id
            // on it would reject every valid one and fail the whole sign-in.
            JwtSecurityToken token = await CreateSut().ValidateTokenAsync(
                Token(Issuer, "https://api.helseid.example"), Provider(strict: true), UpstreamTokenKind.AccessToken, nonce: null);

            Assert.Contains("https://api.helseid.example", token.Audiences);
        }

        [Fact]
        public async Task Strict_IdTokenWithTrailingSlashIssuer_IsRejected()
        {
            await Assert.ThrowsAnyAsync<SecurityTokenInvalidIssuerException>(() => CreateSut().ValidateTokenAsync(
                Token(Issuer + "/", ClientId), Provider(strict: true), UpstreamTokenKind.IdToken, nonce: null));
        }

        [Fact]
        public async Task Strict_AccessTokenWithTrailingSlashIssuer_KeepsHistoricalLeniency()
        {
            // The strict rule is an id_token rule. Access tokens keep the behaviour they always had.
            JwtSecurityToken token = await CreateSut().ValidateTokenAsync(
                Token(Issuer + "/", "https://api.helseid.example"), Provider(strict: true), UpstreamTokenKind.AccessToken, nonce: null);

            Assert.Equal(Issuer + "/", token.Issuer);
        }

        [Fact]
        public async Task NotStrict_IdTokenWithForeignAudience_IsAcceptedAsBefore()
        {
            // Providers that have not opted in are unaffected.
            JwtSecurityToken token = await CreateSut().ValidateTokenAsync(
                Token(Issuer, "some-other-client"), Provider(strict: false), UpstreamTokenKind.IdToken, nonce: null);

            Assert.Contains("some-other-client", token.Audiences);
        }
    }
}
