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

        /// <summary>
        /// Returns (Hash, Salt, Iterations) for storing a new refresh token.
        /// Uses HMAC-SHA256 with the server pepper — O(1), safe for high-entropy random tokens.
        /// Salt is empty and Iterations is 0 for tokens stored with this method.
        /// </summary>
        public static (byte[] Hash, byte[] Salt, int Iterations) HashForStorage(string token, byte[] serverPepper)
        {
            byte[] hash = ComputeLookupKey(token, serverPepper);
            return (hash, Array.Empty<byte>(), 0);
        }

        /// <summary>
        /// Verifies a refresh token against its stored hash.
        /// Supports both legacy PBKDF2 tokens (iterations > 0) and new HMAC tokens (iterations == 0).
        /// </summary>
        public static bool Verify(string token, byte[] serverPepper, byte[] storedHash, byte[] storedSalt, int storedIterations)
        {
            if (storedIterations > 0)
            {
                // Legacy path: token was stored with PBKDF2 before this change
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(token, storedSalt, storedIterations, HashAlgorithmName.SHA256, storedHash.Length);
                return CryptographicOperations.FixedTimeEquals(actual, storedHash);
            }

            // Fast HMAC path: token was stored after this change
            byte[] hmac = ComputeLookupKey(token, serverPepper);
            return CryptographicOperations.FixedTimeEquals(hmac, storedHash);
        }
    }
}
