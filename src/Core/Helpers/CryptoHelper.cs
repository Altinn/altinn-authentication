using System.Security.Cryptography;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    /// <summary>
    /// Helper methods for cryptographic operations.
    /// </summary>
    public static class CryptoHelpers
    {
        /// <summary>
        /// Generates a cryptographically strong random string, base64url-encoded.
        /// </summary>
        /// <param name="byteLength">Number of random bytes to generate. 
        /// The final string length will be ~4/3 of this, without padding.</param>
        public static string RandomBase64Url(int byteLength = 32)
        {
            if (byteLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteLength), "Must be > 0.");
            }

            byte[] buffer = new byte[byteLength];
            RandomNumberGenerator.Fill(buffer);

            string base64 = Convert.ToBase64String(buffer);

            // Convert to base64url (RFC 4648 §5) — no padding, replace +/ with -_
            return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
