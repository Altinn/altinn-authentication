using System.Security.Cryptography;
using System.Text;

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

        /// <summary>
        /// Produces a fast HMAC-SHA256 hash for a high-entropy client secret.
        /// Format: <c>hmac$sha256$&lt;base64hash&gt;</c>. Safe for system-generated random secrets only.
        /// </summary>
        public static string HashHmac(string secret, byte[] serverPepper)
        {
            using var h = new HMACSHA256(serverPepper);
            byte[] hash = h.ComputeHash(Encoding.UTF8.GetBytes(secret));
            return $"{Pbkdf2SecretVerifier.HmacPrefix}{Convert.ToBase64String(hash)}";
        }
    }
}
