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
        /// When enabled, disables the cookie ticket decryption path (SBL Bridge
        /// <c>POST authentication/api/tickets</c>). No replacement exists; the A2 cookie→token
        /// flow will be removed entirely after the SBL Bridge decommission (deadline 2026-06-19).
        /// Tracked in issue #2008.
        /// </summary>
        public const string CookieTicketDecryptionDisabled = "CookieTicketDecryptionDisabled";

        /// <summary>
        /// When enabled, disables the enterprise-user authentication path (SBL Bridge
        /// <c>POST authentication/api/enterpriseuser</c>) and returns HTTP 410 Gone with a
        /// problem-details body pointing callers to Systembruker or ID-porten. Tracked in
        /// issue #2008 (SBL Bridge decommission, deadline 2026-06-19).
        /// </summary>
        public const string EnterpriseUserAuthenticationDisabled = "EnterpriseUserAuthenticationDisabled";
    }
}
