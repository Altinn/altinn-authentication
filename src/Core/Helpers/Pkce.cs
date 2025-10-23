using System.Security.Cryptography;
using System.Text;

namespace Altinn.Platform.Authentication.Core.Helpers
{ 
    public static class Pkce
    {
        // Allowed characters for code_verifier per RFC 7636
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

        /// <summary>
        /// Generates a PKCE code_verifier (RFC 7636) using the allowed charset.
        /// Length must be between 43 and 128 characters (inclusive).
        /// </summary>
        public static string RandomPkceVerifier(int length = 64)
        {
            if (length < 43 || length > 128)
                throw new ArgumentOutOfRangeException(nameof(length), "PKCE code_verifier length must be 43–128.");

            Span<char> chars = length <= 128 ? stackalloc char[length] : new char[length];

            for (int i = 0; i < length; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)];

            return new string(chars);
        }

        /// <summary>
        /// Computes S256 code_challenge = BASE64URL(SHA256(code_verifier)).
        /// </summary>
        public static string ComputeS256CodeChallenge(string codeVerifier)
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new ArgumentException("code_verifier is required.", nameof(codeVerifier));

            return Hashing.Sha256Base64Url(codeVerifier);
        }

        public static bool VerifyS256(string storedChallenge, string incomingVerifier)
        {
            // verifier charset & length per RFC 7636 (43..128; ALPHA / DIGIT / "-" / "." / "_" / "~")
            if (string.IsNullOrEmpty(storedChallenge)) return false;
            if (string.IsNullOrWhiteSpace(incomingVerifier)) return false;
            if (incomingVerifier.Length is < 43 or > 128) return false;
            foreach (var c in incomingVerifier)
            {
                bool ok = char.IsLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
                if (!ok) return false;
            }

            string computed = Hashing.Sha256Base64Url(incomingVerifier);
            
            // Constant-time comparison to prevent timing attacks
            if (storedChallenge.Length != computed.Length)
            {
                return false;
            }
            
            byte[] storedBytes = Encoding.ASCII.GetBytes(storedChallenge);
            byte[] computedBytes = Encoding.ASCII.GetBytes(computed);
            return CryptographicOperations.FixedTimeEquals(storedBytes, computedBytes);
        }
    }
}
