using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Mockporten.Configuration;
using Mockporten.Services.Implementation;
using Xunit;

namespace Mockporten.Tests
{
    /// <summary>
    /// The `acr` claim must be a single fulfilled LoA. altinn-authentication maps
    /// it via GetAuthenticationLevelForIdPorten, which matches one token — a
    /// multi-valued or empty acr falls through to the lowest level. Regression
    /// guard for the acr-level breakage.
    /// </summary>
    public class TokenServiceAcrTests
    {
        private static TokenService NewService() =>
            new TokenService(
                Options.Create(new GeneralSettings { IssCode = "https://test-idp.example" }),
                certificateProvider: null,
                logger: null);

        private static string? AcrOf(ClaimsPrincipal p) =>
            p.Claims.FirstOrDefault(c => c.Type == "acr")?.Value;

        [Fact]
        public void GetClaimsPrincipal_NullAcr_DefaultsToHighLevel()
        {
            ClaimsPrincipal p = NewService().GetClaimsPrincipal(
                "sub", "01818520030", "nb", "n", "sid", "aud", null, new[] { "bankid" }, DateTimeOffset.UtcNow);

            Assert.Equal("Level4", AcrOf(p));
        }

        [Fact]
        public void GetClaimsPrincipal_EmptyAcrEntries_DefaultsToHighLevel()
        {
            // "".Split(" ") yields [""]; this must not produce an empty acr claim.
            ClaimsPrincipal p = NewService().GetClaimsPrincipal(
                "sub", "01818520030", "nb", "n", "sid", "aud", "".Split(" "), new[] { "bankid" }, DateTimeOffset.UtcNow);

            Assert.Equal("Level4", AcrOf(p));
        }

        [Fact]
        public void GetClaimsPrincipal_SingleAcr_PassesThrough()
        {
            ClaimsPrincipal p = NewService().GetClaimsPrincipal(
                "sub", "01818520030", "nb", "n", "sid", "aud", new[] { "idporten-loa-high" }, new[] { "bankid" }, DateTimeOffset.UtcNow);

            Assert.Equal("idporten-loa-high", AcrOf(p));
        }
    }
}
