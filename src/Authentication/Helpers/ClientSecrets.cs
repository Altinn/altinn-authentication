#nullable enable

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper for verifying client secrets.
    /// </summary>
    public static class ClientSecrets
    {
        /// <summary>
        /// verifies a presented secret against a stored hash.
        /// </summary>
        public static bool Verify(string? storedHash, string presentedSecret)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            // TODO: plug your argon2/bcrypt verification here
            // return BCrypt.Net.BCrypt.Verify(presentedSecret, storedHash);
            return SlowEquals(storedHash, presentedSecret); // placeholder: replace!
        }

        private static bool SlowEquals(string a, string b)
        {
            // constant-time compare stub, replace with real hash verify
            if (a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
