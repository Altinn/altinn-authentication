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
