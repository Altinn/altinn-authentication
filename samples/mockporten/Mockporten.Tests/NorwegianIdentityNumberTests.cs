using Mockporten.Helpers;
using Xunit;

namespace Mockporten.Tests
{
    public class NorwegianIdentityNumberTests
    {
        // Synthetic (Tenor) fnr: month + 80. Constructed and mod11-verified:
        //  - 01818520030: born 01.01.1985, MM=81, valid control digits.
        //  - 41818520024: same date as a synthetic D-number (day 41 = real day 1),
        //    with recomputed mod11 control digits (K1=2, K2=4).
        [Theory]
        [InlineData("01818520030")]
        [InlineData("41818520024")]
        public void IsSyntheticTenorPid_ValidSynthetic_ReturnsTrue(string pid)
        {
            Assert.True(NorwegianIdentityNumber.IsSyntheticTenorPid(pid));
        }

        [Theory]
        // Ordinary fnr (month 01-12) must NEVER pass - the core invariant.
        [InlineData("01018520030")] // same number, real month 01
        [InlineData("01017012345")]
        [InlineData("31125099999")]
        // Real D-number (day+40, month 01-12) must NEVER pass.
        [InlineData("41010012345")]
        // H-number (month+40 -> 41-52) is not synthetic.
        [InlineData("01418520030")]
        // Synthetic month but broken mod11 control digits.
        [InlineData("01818520031")]
        // Format failures.
        [InlineData("")]
        [InlineData("0181852003")]   // 10 digits
        [InlineData("018185200300")] // 12 digits
        [InlineData("0181852003X")]  // non-digit
        [InlineData("01938520030")]  // month 93 (out of synthetic range)
        [InlineData("01808520030")]  // month 80 (out of synthetic range)
        public void IsSyntheticTenorPid_NonSynthetic_ReturnsFalse(string pid)
        {
            Assert.False(NorwegianIdentityNumber.IsSyntheticTenorPid(pid));
        }

        [Fact]
        public void IsSyntheticTenorPid_Null_ReturnsFalse()
        {
            Assert.False(NorwegianIdentityNumber.IsSyntheticTenorPid(null));
        }
    }
}
