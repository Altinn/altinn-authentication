using System.Security.Cryptography;
using System.Text;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    public static class Hashing
    {
        /// <summary>
        /// SHA-256 hash of the UTF-8 bytes of <paramref name="input"/>,
        /// returned as Base64URL (no padding).
        /// </summary>
        public static string Sha256Base64Url(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return Sha256Base64Url(Encoding.UTF8.GetBytes(input));
        }

        /// <summary>
        /// SHA-256 hash of <paramref name="data"/>, returned as Base64URL (no padding).
        /// </summary>
        public static string Sha256Base64Url(ReadOnlySpan<byte> data)
        {
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(data, hash);
            var b64 = Convert.ToBase64String(hash);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
