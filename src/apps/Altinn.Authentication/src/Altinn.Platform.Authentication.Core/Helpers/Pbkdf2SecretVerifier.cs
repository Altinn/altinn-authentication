using System.Security.Cryptography;
using System.Text;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    public static class Pbkdf2SecretVerifier
    {
        public const string HmacPrefix = "hmac$sha256$";

        public static bool Verify(string secret, string stored, byte[] serverPepper)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(stored))
            {
                return false;
            }

            if (stored.StartsWith(HmacPrefix, StringComparison.Ordinal))
            {
                return VerifyHmac(secret, stored, serverPepper);
            }

            return VerifyPbkdf2(secret, stored);
        }

        public static bool IsLegacy(string? stored) =>
            !string.IsNullOrEmpty(stored) && !stored!.StartsWith(HmacPrefix, StringComparison.Ordinal);

        public static bool NeedsRehash(string stored, int targetIterations)
        {
            if (stored.StartsWith(HmacPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = stored.Split('$');
            if (parts.Length != 5 || !parts[2].StartsWith("i=")) return true;
            return !int.TryParse(parts[2].AsSpan(2), out int iters) || iters < targetIterations;
        }

        private static bool VerifyHmac(string secret, string stored, byte[] serverPepper)
        {
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(stored.AsSpan(HmacPrefix.Length).ToString());
            }
            catch
            {
                return false;
            }

            using var h = new HMACSHA256(serverPepper);
            byte[] actual = h.ComputeHash(Encoding.UTF8.GetBytes(secret));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static bool VerifyPbkdf2(string secret, string stored)
        {
            var parts = stored.Split('$');
            if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256" || !parts[2].StartsWith("i="))
                return false;

            if (!int.TryParse(parts[2].AsSpan(2), out int iterations) || iterations <= 0)
                return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expected = Convert.FromBase64String(parts[4]);
            }
            catch
            {
                return false;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password: secret,
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
