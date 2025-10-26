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
        /// verifies a presented secret against a stored hash.
        /// </summary>
        public static bool Verify(string? storedHash, string presentedSecret)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(presentedSecret))
            {
                return false;
            }

            return Pbkdf2SecretVerifier.Verify(presentedSecret, storedHash);
        }
    }
}
