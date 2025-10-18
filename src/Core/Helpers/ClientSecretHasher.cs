using System.Security.Cryptography;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    public static class ClientSecretHasher
    {
        public static string Hash(string secret, int iterations = 600_000, int saltSize = 16, int dkLen = 32)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(saltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password: secret,
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: dkLen);

            return $"pbkdf2$sha256$i={iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string secret, string stored)
        {
            // Format: pbkdf2$sha256$i=ITER$SALTB64$HASHB64
            var parts = stored.Split('$');
            if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256") return false;

            int iterations = int.Parse(parts[2].AsSpan(2)); // skip "i="
            byte[] salt = Convert.FromBase64String(parts[3]);
            byte[] expected = Convert.FromBase64String(parts[4]);

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
