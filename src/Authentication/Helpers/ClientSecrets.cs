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
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            // TODO: Verify hashing algorithm from storedHash prefix if needed
            return Pbkdf2SecretVerifier.Verify(presentedSecret, storedHash);
        }
    }
}
