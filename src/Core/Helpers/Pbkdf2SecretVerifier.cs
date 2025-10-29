using System.Security.Cryptography;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    public static class Pbkdf2SecretVerifier
    {
        // Validate "pbkdf2$sha256$i=<iterations>$<saltB64>$<hashB64>"
        public static bool Verify(string secret, string stored)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(stored))
                return false;

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
                return false; // bad base64
            }

            // Derive with the same params
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password: secret,
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // Optional: decide if you should rehash (e.g., after raising iterations)
        public static bool NeedsRehash(string stored, int targetIterations)
        {
            var parts = stored.Split('$');
            if (parts.Length != 5 || !parts[2].StartsWith("i=")) return true;
            return !int.TryParse(parts[2].AsSpan(2), out int iters) || iters < targetIterations;
        }
    }
}
