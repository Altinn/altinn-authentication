#nullable enable

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Names of the claims that carry each piece of meaning in a provider's id_token.
    /// Defaults are ID-porten's, so a provider that follows ID-porten's conventions needs no
    /// configuration at all.
    /// </summary>
    public class OidcClaimMappings
    {
        /// <summary>
        /// Claim carrying the national identity number.
        /// ID-porten: <c>pid</c>. HelseID: <c>helseid://claims/identity/pid</c>.
        /// </summary>
        public string Pid { get; set; } = "pid";

        /// <summary>
        /// Claim carrying the authentication level.
        /// ID-porten: <c>acr</c>. HelseID: <c>helseid://claims/identity/security_level</c>.
        /// </summary>
        public string AuthLevel { get; set; } = "acr";

        /// <summary>
        /// Claim carrying the authentication method. May appear more than once in a token.
        /// ID-porten: <c>amr</c>. HelseID: <c>idp</c> — its <c>amr</c> is the Duende default
        /// <c>["pwd"]</c> and says nothing about which eID was actually used.
        /// </summary>
        public string AuthMethod { get; set; } = "amr";

        /// <summary>
        /// Claim carrying the user's email address.
        /// </summary>
        public string Email { get; set; } = "email";
    }
}
