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
    }
}
