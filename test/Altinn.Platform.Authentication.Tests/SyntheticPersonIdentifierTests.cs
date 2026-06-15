using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using System.Security.Claims;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Xunit;

namespace Altinn.Platform.Authentication.Tests
{
    public class SyntheticPersonIdentifierTests
    {
        // 01818520030: born 01.01.1985, MM=81 (synthetic), valid mod11.
        private const string SyntheticPid = "01818520030";

        [Theory]
        [InlineData("01818520030")] // synthetic (born 01.01.1985, MM=81)
        [InlineData("41818520024")] // synthetic D-number (day 41 = real day 1, MM=81), valid mod11
        public void IsSyntheticTenor_ValidSynthetic_ReturnsTrue(string pid)
        {
            Assert.True(SyntheticPersonIdentifier.IsSyntheticTenor(pid));
        }

        [Theory]
        [InlineData("01018520030")] // same number, real month 01
        [InlineData("01017012345")] // ordinary
        [InlineData("41010012345")] // real D-number (month 01-12)
        [InlineData("01418520030")] // H-number (month 41-52)
        [InlineData("01818520031")] // synthetic month, broken mod11
        [InlineData("01938520030")] // month 93 (out of range)
        [InlineData("01808520030")] // month 80 (out of range)
        [InlineData("")]
        [InlineData("0181852003")] // 10 digits
        [InlineData("018185200300")] // 12 digits
        [InlineData("0181852003X")] // non-digit
        public void IsSyntheticTenor_NonSynthetic_ReturnsFalse(string pid)
        {
            Assert.False(SyntheticPersonIdentifier.IsSyntheticTenor(pid));
        }

        [Fact]
        public void IsSyntheticTenor_Null_ReturnsFalse()
        {
            Assert.False(SyntheticPersonIdentifier.IsSyntheticTenor(null));
        }

        private static JwtSecurityToken TokenWithPid(string pid)
        {
            var claims = new List<Claim>();
            if (pid != null)
            {
                claims.Add(new Claim("pid", pid));
            }

            return new JwtSecurityToken(issuer: "https://test-idp.example", claims: claims);
        }

        [Fact]
        public void GetUserFromToken_RequireSyntheticPid_SyntheticPid_IsAuthenticated()
        {
            var provider = new OidcProvider { IssuerKey = "mockporten", RequireSyntheticPid = true };

            var result = AuthenticationHelper.GetUserFromToken(TokenWithPid(SyntheticPid), provider);

            Assert.True(result.IsAuthenticated);
            Assert.Equal(SyntheticPid, result.SSN);
        }

        [Fact]
        public void GetUserFromToken_RequireSyntheticPid_OrdinaryPid_Throws()
        {
            var provider = new OidcProvider { IssuerKey = "mockporten", RequireSyntheticPid = true };

            // Throws (rather than returning not-authenticated) so the request
            // is guaranteed to abort and cannot be mishandled downstream.
            Assert.Throws<AuthenticationException>(
                () => AuthenticationHelper.GetUserFromToken(TokenWithPid("01017012345"), provider));
        }

        [Fact]
        public void GetUserFromToken_RequireSyntheticPidFalse_OrdinaryPid_StillAuthenticated()
        {
            var provider = new OidcProvider { IssuerKey = "idporten", RequireSyntheticPid = false };

            var result = AuthenticationHelper.GetUserFromToken(TokenWithPid("01017012345"), provider);

            Assert.True(result.IsAuthenticated);
        }

        [Fact]
        public void GetUserFromToken_RequireSyntheticPid_NoPid_NotAffected()
        {
            var provider = new OidcProvider { IssuerKey = "mockporten", RequireSyntheticPid = true };

            var result = AuthenticationHelper.GetUserFromToken(TokenWithPid(null), provider);

            // The gate only applies to a pid claim; absence of pid is unchanged.
            Assert.True(result.IsAuthenticated);
        }
    }
}
