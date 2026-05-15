#nullable enable
using Altinn.Platform.Authentication.Core.Helpers;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper for verifying client secrets.
    /// </summary>
    public static class ClientSecrets
    {
        /// <summary>
        /// Verifies a presented secret against a stored hash.
        /// Supports legacy PBKDF2 hashes and new HMAC-SHA256 hashes; the verifier dispatches on prefix.
        /// </summary>
        public static bool Verify(string? storedHash, string presentedSecret, byte[] serverPepper)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(presentedSecret))
            {
                return false;
            }

            return Pbkdf2SecretVerifier.Verify(presentedSecret, storedHash, serverPepper);
        }
    }
}
