using System;
using System.Security.Cryptography;
using System.Text;

namespace Mockporten.Helpers
{
    /// <summary>
    /// RFC 7636 PKCE helpers. Stateless - the code_challenge is carried inside
    /// the (signed) authorization code, and verified here against the
    /// code_verifier presented at the token endpoint. See issue #1983 / #1409.
    /// </summary>
    public static class Pkce
    {
        public const string MethodS256 = "S256";

        /// <summary>
        /// Computes the S256 code_challenge for a code_verifier:
        /// BASE64URL(SHA256(ASCII(code_verifier))).
        /// </summary>
        public static string ComputeS256Challenge(string codeVerifier)
        {
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64Url(hash);
        }

        /// <summary>
        /// Verifies a code_verifier against a stored code_challenge.
        /// Only S256 is accepted (plain is intentionally rejected).
        /// Constant-time comparison; any missing/blank input returns false.
        /// </summary>
        public static bool Verify(string codeChallenge, string codeChallengeMethod, string codeVerifier)
        {
            if (string.IsNullOrEmpty(codeChallenge) ||
                string.IsNullOrEmpty(codeVerifier) ||
                !MethodS256.Equals(codeChallengeMethod, StringComparison.Ordinal))
            {
                return false;
            }

            string computed = ComputeS256Challenge(codeVerifier);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(computed),
                Encoding.ASCII.GetBytes(codeChallenge));
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
