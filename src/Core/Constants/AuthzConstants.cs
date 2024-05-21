namespace Altinn.Platform.Authentication.Core.Constants
{
    /// <summary>
    /// Constants related to Authorization
    /// </summary>
    public static class AuthzConstants
    {
        /// <summary>
        /// Policy tag for authorizing Altinn.Platform.Authorization API access from AltinnII Authorization
        /// </summary>
        public const string ALTINNII_AUTHORIZATION = "AltinnIIAuthorizationAccess";

        /// <summary>
        /// Policy tag for authorizing internal Altinn.Platform.Authorization API access
        /// </summary>
        public const string INTERNAL_AUTHORIZATION = "InternalAuthorizationAccess";

        /// <summary>
        /// Policy tag for reading an maskinporten delegation
        /// </summary>
        public const string POLICY_MASKINPORTEN_DELEGATION_READ = "MaskinportenDelegationRead";

        /// <summary>
        /// Policy tag for writing an maskinporten delegation
        /// </summary>
        public const string POLICY_MASKINPORTEN_DELEGATION_WRITE = "MaskinportenDelegationWrite";

        /// <summary>
        /// Policy tag for reading access management information
        /// </summary>
        public const string POLICY_ACCESS_MANAGEMENT_READ = "AccessManagementRead";

        /// <summary>
        /// Policy tag for writing access management delegations
        /// </summary>
        public const string POLICY_ACCESS_MANAGEMENT_WRITE = "AccessManagementWrite";

        /// <summary>
        /// Policy tag for scope authorization on the proxy API from Altinn II for the maskinporten integration API
        /// </summary>
        public const string POLICY_MASKINPORTEN_DELEGATIONS_PROXY = "MaskinportenDelegationsProxy";

        /// <summary>
        /// Policy tag for scope authorization on the resource owner API for getting the Authorized Party list for a third party
        /// </summary>
        public const string POLICY_RESOURCEOWNER_AUTHORIZEDPARTIES = "ResourceOwnerAuthorizedParty";

        /// <summary>
        /// Scope giving access to getting authorized parties for a third party, for which the third party have access to one or more of the resource owners services, apps or resources.
        /// </summary>
        public const string SCOPE_RESOURCEOWNER_AUTHORIZEDPARTIES = "altinn:resourceowner/authorizedparties";

        /// <summary>
        /// Scope giving access to getting all authorized parties for a third party
        /// </summary>
        public const string SCOPE_RESOURCEOWNER_AUTHORIZEDPARTIES_ADMIN = "altinn:resourceowner/authorizedparties.admin";

        /// <summary>
        /// Scope giving access to delegations for Maskinporten schemes owned by authenticated party 
        /// </summary>
        public const string SCOPE_MASKINPORTEN_DELEGATIONS = "altinn:maskinporten/delegations";

        /// <summary>
        /// Scope giving access to delegations for arbitrary Maskinporten schemes
        /// </summary>
        public const string SCOPE_MASKINPORTEN_DELEGATIONS_ADMIN = "altinn:maskinporten/delegations.admin";

        /// <summary>
        /// Claim for scopes from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_SCOPE = "scope";

        /// <summary>
        /// Claim for full consumer from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_CONSUMER = "consumer";

        /// <summary>
        /// Claim for consumer prefixes from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_CONSUMER_PREFIX = "consumer_prefix";
    }
}
