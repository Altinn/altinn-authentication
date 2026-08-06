using Mockporten.Helpers;
using Xunit;

namespace Mockporten.Tests
{
    public class PkceTests
    {
        // RFC 7636 Appendix B test vector.
        private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        [Fact]
        public void ComputeS256Challenge_MatchesRfc7636Vector()
        {
            Assert.Equal(Challenge, Pkce.ComputeS256Challenge(Verifier));
        }

        [Fact]
        public void Verify_CorrectVerifier_ReturnsTrue()
        {
            Assert.True(Pkce.Verify(Challenge, "S256", Verifier));
        }

        [Theory]
        [InlineData("wrong-verifier")]
        [InlineData("")]
        public void Verify_WrongOrMissingVerifier_ReturnsFalse(string verifier)
        {
            Assert.False(Pkce.Verify(Challenge, "S256", verifier));
        }

        [Theory]
        [InlineData("plain")]
        [InlineData("")]
        [InlineData("s256")] // case-sensitive: only "S256"
        public void Verify_NonS256Method_ReturnsFalse(string method)
        {
            Assert.False(Pkce.Verify(Challenge, method, Verifier));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-the-challenge")]
        public void Verify_MissingOrWrongChallenge_ReturnsFalse(string challenge)
        {
            Assert.False(Pkce.Verify(challenge, "S256", Verifier));
        }

        [Fact]
        public void Verify_NullInputs_ReturnFalse()
        {
            Assert.False(Pkce.Verify(null, "S256", Verifier));
            Assert.False(Pkce.Verify(Challenge, null, Verifier));
            Assert.False(Pkce.Verify(Challenge, "S256", null));
        }
    }
}
