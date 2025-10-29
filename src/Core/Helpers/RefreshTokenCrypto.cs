using System.Security.Cryptography;
using System.Text;

namespace Altinn.Platform.Authentication.Core.Helpers
{

    public static class RefreshTokenCrypto
    {
        // serverPepper must come from secure config (KeyVault)
        public static byte[] ComputeLookupKey(string token, byte[] serverPepper)
        {
            using var h = new HMACSHA256(serverPepper);
            return h.ComputeHash(Encoding.UTF8.GetBytes(token));
        }

        public static (byte[] Hash, byte[] Salt, int Iterations) HashForStorage(
            string token, int iterations = 300_000, int saltSize = 16, int dkLen = 32)
        {
            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(token, salt, iterations, HashAlgorithmName.SHA256, dkLen);
            return (hash, salt, iterations);
        }

        public static bool Verify(string token, byte[] salt, int iterations, byte[] expected)
        {
            var actual = Rfc2898DeriveBytes.Pbkdf2(token, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
