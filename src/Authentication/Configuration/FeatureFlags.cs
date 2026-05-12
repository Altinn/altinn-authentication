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
        /// <c>POST /register/api/v2/internal/users/self-identified</c> endpoint instead of
        /// calling SBL Bridge directly. Replaces the GetUser+CreateUser two-call flow with
        /// one atomic call. Tracked under the SBL Bridge decommission (deadline 2026-06-19).
        /// </summary>
        public const string RegisterSelfIdentifiedUserProvisioning = "RegisterSelfIdentifiedUserProvisioning";
    }
}
