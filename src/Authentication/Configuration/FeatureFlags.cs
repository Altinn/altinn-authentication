namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Feature flags 
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// audit log flag
        /// </summary>
        public const string AuditLog = "AuditLog";

        /// <summary>
        /// Feature flag for SystemUser Controller and functionality
        /// </summary>
        public const string SystemUser = "SystemUser";

        /// <summary>
        /// When enabled, OIDC sign-in provisions self-identified users via register's
        /// <c>POST /register/api/v2/internal/parties/self-identified</c> endpoint instead of
        /// calling SBL Bridge directly. Replaces the GetUser+CreateUser two-call flow with
        /// one atomic call. Tracked under the SBL Bridge decommission (deadline 2026-06-19).
        /// </summary>
        public const string RegisterSelfIdentifiedUserProvisioning = "RegisterSelfIdentifiedUserProvisioning";

        /// <summary>
        /// When enabled, self-identified (SI) user credential validation is performed locally
        /// against <c>oidcserver.selfidentified_user_credential</c> (SHA1 + salt) instead of
        /// calling SBL Bridge (<c>POST authentication/api/siuser</c>). Lets the flag be flipped
        /// per environment once the migrated credentials are imported. Tracked in issue #2025
        /// (SBL Bridge decommission, deadline 2026-06-19).
        /// </summary>
        public const string LocalSelfIdentifiedCredentialValidation = "LocalSelfIdentifiedCredentialValidation";

        /// <summary>
        /// Controls the source of the user fields (UserId/UserName/PartyId/PartyUuid) in the
        /// ID-porten token exchange, replacing the (decommissioned) SBL Bridge
        /// <c>profile/users/</c> lookup. When enabled, the fields are read from Register's
        /// <c>POST /register/api/v2/internal/parties/query</c> endpoint; when disabled, from the
        /// platform Profile API (<c>internal/user</c>). Tracked under the SBL Bridge
        /// decommission (deadline 2026-06-19).
        /// </summary>
        public const string IdPortenUserLookupFromRegister = "IdPortenUserLookupFromRegister";
    }
}
