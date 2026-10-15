using System;
using System.Globalization;
using Altinn.Register.Contracts;

#nullable enable

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Detects synthetic (Tenor / Skatteetaten test) Norwegian national identity
    /// numbers (fødselsnummer).
    ///
    /// Validity (11 digits + mod11 control digits, old and new algorithm) is
    /// delegated to <see cref="PersonIdentifier"/> from
    /// <c>Altinn.Register.Contracts</c>. On top of that, synthetic test persons
    /// are marked by adding 80 to the month, i.e. <c>MM</c> in the range 81–92.
    ///
    /// <see cref="IsSyntheticTenor"/> is a <b>positive, fail-closed</b> check: it
    /// returns true ONLY for a valid fnr whose month is in 81–92. Any invalid or
    /// non-synthetic number (every ordinary fnr and real D-number, month 01–12)
    /// returns false. See issue #1409 / #1983.
    /// </summary>
    public static class SyntheticPersonIdentifier
    {
        /// <summary>
        /// True only for a valid synthetic (Tenor) fødselsnummer. Any other
        /// input — including every ordinary fnr and every real D-number — returns
        /// false (fail-closed).
        /// </summary>
        public static bool IsSyntheticTenor(string? pid)
        {
            // Validity: 11 digits + mod11 control digits.
            if (!PersonIdentifier.TryParse(pid, provider: null, out _))
            {
                return false;
            }

            // Synthetic marker: the month component (digits 3-4) has 80 added,
            // i.e. MM in 81–92. pid is guaranteed to be 11 digits here.
            int month = int.Parse(pid!.AsSpan(2, 2), CultureInfo.InvariantCulture);
            return month is >= 81 and <= 92;
        }
    }
}
