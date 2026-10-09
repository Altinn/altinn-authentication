using System;
using System.Linq;

namespace Mockporten.Helpers
{
    /// <summary>
    /// Validation of Norwegian national identity numbers (fødselsnummer).
    ///
    /// An fnr is 11 digits: DD MM YY III KK. Tenor / Skatteetaten <b>synthetic</b>
    /// test persons are marked by adding 80 to the month, i.e. <c>MM</c> in the
    /// range 81–92 (real month = MM − 80). A synthetic person may additionally be
    /// a D-number (day + 40, <c>DD</c> in 41–71); the synthetic marker is always
    /// the month + 80.
    ///
    /// <see cref="IsSyntheticTenorPid"/> is a <b>positive, fail-closed</b> check:
    /// it returns true ONLY for a well-formed synthetic Tenor fnr. Any parse,
    /// format or mod11 failure returns false. An ordinary fnr and a real D-number
    /// (month 01–12) fail the <c>MM ∈ 81–92</c> test unconditionally, so a real
    /// identity can never pass. See issue #1983 / #1409.
    /// </summary>
    public static class NorwegianIdentityNumber
    {
        // mod11 weights for the two control digits.
        private static readonly int[] K1Weights = { 3, 7, 6, 1, 8, 9, 4, 5, 2 };
        private static readonly int[] K2Weights = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

        /// <summary>
        /// True only for a well-formed synthetic (Tenor) fødselsnummer.
        /// Any other input — including every ordinary fnr and every real
        /// D-number — returns false (fail-closed).
        /// </summary>
        public static bool IsSyntheticTenorPid(string pid)
        {
            if (pid is null || pid.Length != 11 || !pid.All(char.IsDigit))
            {
                return false;
            }

            int day = int.Parse(pid.Substring(0, 2));
            int month = int.Parse(pid.Substring(2, 2));

            // The synthetic marker: month + 80. This is the invariant a real
            // identity can never satisfy.
            if (month is < 81 or > 92)
            {
                return false;
            }

            int normalizedDay = day > 40 ? day - 40 : day; // allow synthetic D-number
            int normalizedMonth = month - 80;

            if (normalizedDay is < 1 or > 31 || normalizedMonth is < 1 or > 12)
            {
                return false;
            }

            return HasValidControlDigits(pid);
        }

        /// <summary>
        /// Validates the two mod11 control digits of an 11-digit fnr.
        /// </summary>
        private static bool HasValidControlDigits(string pid)
        {
            int[] d = pid.Select(c => c - '0').ToArray();

            int k1 = Mod11(d, K1Weights, 9);
            if (k1 == 10 || k1 != d[9])
            {
                return false;
            }

            int k2 = Mod11(d, K2Weights, 10);
            if (k2 == 10 || k2 != d[10])
            {
                return false;
            }

            return true;
        }

        private static int Mod11(int[] digits, int[] weights, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += digits[i] * weights[i];
            }

            int remainder = sum % 11;
            return remainder == 0 ? 0 : 11 - remainder;
        }
    }
}
