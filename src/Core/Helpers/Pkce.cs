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
            Span<byte> buf = stackalloc byte[length];
            RandomNumberGenerator.Fill(buf);

            for (int i = 0; i < length; i++)
                chars[i] = Alphabet[buf[i] % Alphabet.Length];

            return new string(chars);
        }

        /// <summary>
        /// Computes S256 code_challenge = BASE64URL(SHA256(code_verifier)).
        /// </summary>
        public static string ComputeS256CodeChallenge(string codeVerifier)
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new ArgumentException("code_verifier is required.", nameof(codeVerifier));

            byte[] bytes = Encoding.ASCII.GetBytes(codeVerifier); // RFC 7636 uses ASCII subset
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(bytes, hash);

            string b64 = Convert.ToBase64String(hash);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static bool VerifyS256(string storedChallenge, string incomingVerifier)
        {
            // verifier charset & length per RFC 7636 (43..128; ALPHA / DIGIT / "-" / "." / "_" / "~")
            if (string.IsNullOrWhiteSpace(incomingVerifier)) return false;
            if (incomingVerifier.Length is < 43 or > 128) return false;
            foreach (var c in incomingVerifier)
            {
                bool ok = char.IsLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
                if (!ok) return false;
            }

            var computed = Hashing.Sha256Base64Url(incomingVerifier);
            return string.Equals(storedChallenge, computed, StringComparison.Ordinal);
        }
    }
}
