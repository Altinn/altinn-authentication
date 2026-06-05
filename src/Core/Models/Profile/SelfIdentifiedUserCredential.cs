namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// A self-identified (SI) user credential migrated from Altinn 2, stored in
    /// <c>oidcserver.selfidentified_user_credential</c>. Used to validate SI logins locally
    /// (SHA1 + salt) instead of calling SBL Bridge (<c>authentication/api/siuser</c>). See issue #2025.
    /// </summary>
    public class SelfIdentifiedUserCredential
    {
        /// <summary>
        /// Gets or sets the Altinn 3 party UUID of the user. Returned on successful validation.
        /// </summary>
        public Guid PartyUuid { get; set; }

        /// <summary>
        /// Gets or sets the legacy user id (matches <c>oidc_session.subject_user_id</c>).
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the username (login key, matches <c>oidc_session.subject_user_name</c>).
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the Base64-encoded SHA1 password hash, copied verbatim from Altinn 2.
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets the Base64-encoded salt, copied verbatim from Altinn 2.
        /// </summary>
        public string Salt { get; set; }

        /// <summary>
        /// Gets or sets the password expiry timestamp.
        /// </summary>
        public DateTimeOffset PasswordExpiry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the credential is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the source Altinn 2 user id (<c>AUTHN_UserProfile.uid</c>), for traceability.
        /// </summary>
        public int? Altinn2UserId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp the row was imported into Altinn 3.
        /// </summary>
        public DateTimeOffset ImportedAt { get; set; }
    }
}
