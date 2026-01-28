using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Constants
{
    /// <summary>
    /// Constants related to authorization
    /// </summary>
    public static class AuthzConstants
    {
        /// <summary>
        /// Policy tag for authorizing write scope.
        /// </summary>
        public const string POLICY_SCOPE_SYSTEMREGISTER_WRITE = "ScopeSystemRegisterWrite";

        /// <summary>
        /// Scope for system register write scope.
        /// </summary>
        public const string SCOPE_SYSTEMREGISTER_WRITE = "altinn:authentication/systemregister.write";

        /// <summary>
        /// Scope for system register admin access
        /// </summary>
        public const string SCOPE_SYSTEMREGISTER_ADMIN = "altinn:authentication/systemregister.admin";

        /// <summary>
        /// Claim for scopes from maskinporten token
        /// </summary>
        public const string CLAIM_SCOPE = "scope";

        /// <summary>
        /// Claim for consumer prefixes from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_CONSUMER_PREFIX = "consumer_prefix";

        /// <summary>
        /// Policy tag for reading access management information
        /// </summary>
        public const string POLICY_ACCESS_MANAGEMENT_READ = "AccessManagementRead";

        /// <summary>
        /// Policy tag for writing access management delegations
        /// </summary>
        public const string POLICY_ACCESS_MANAGEMENT_WRITE = "AccessManagementWrite";

        /// <summary>
        /// Policy tag for authorizing write scope for system user request
        /// </summary>
        public const string POLICY_SCOPE_SYSTEMUSERREQUEST_WRITE = "ScopeSystemUserRequestWrite";

        /// <summary>
        /// Scope for system user write scope.
        /// </summary>
        public const string SCOPE_SYSTEMUSER_REQUEST_WRITE = "altinn:authentication/systemuser.request.write";

        /// <summary>
        /// Policy tag for authorizing read scope for system user request
        /// </summary>
        public const string POLICY_SCOPE_SYSTEMUSERREQUEST_READ = "ScopeSystemUserRequestRead";

        /// <summary>
        /// Scope for system user read scope.
        /// </summary>
        public const string SCOPE_SYSTEMUSER_REQUEST_READ = "altinn:authentication/systemuser.request.read";

        /// <summary>
        /// Policy used for endpoint where maskinporten can 
        /// </summary>
        public const string POLICY_SCOPE_SYSTEMUSERLOOKUP = "ScopeSystemUserLookup";

        /// <summary>
        /// Scope set by maskinporten for system user lookup
        /// </summary>
        public const string SCOPE_SYSTEMUSER_LOOKUP = "altinn:maskinporten/systemuser.read";

        /// <summary>
        /// Policy tag for authorizing write scope.
        /// </summary>
        public const string POLICY_SCOPE_PORTAL = "ScopePortal";

        /// <summary>
        /// General scope given to everyone loging in to altinn platform 
        /// </summary>
        public const string SCOPE_PORTAL = "altinn:portal/enduser";

        /// <summary>
        /// Used for internal APIs
        /// </summary>
        public const string POLICY_SCOPE_INTERNAL_OR_PLATFORM_ACCESS = "InternalOrPlatformAccess";

        /// <summary>
        /// Used for internal SystemUser APIs
        /// </summary>
        public const string SCOPE_INTERNAL_OR_PLATFORM_ACCESS = "altinn:authentication/systemuser.admin";

        /// <summary>
        /// Issuer when session is created based on Altinn 2 Ticket
        /// </summary>
        public const string ISSUER_ALTINN_PORTAL = "SBL";

        /// <summary>
        /// Policy tag for reading client delegation information
        /// </summary>
        public const string POLICY_CLIENTDELEGATION_READ = "ClientDelegationRead";

        /// <summary>
        /// Scope for reading client delegation information
        /// </summary>
        public const string SCOPE_CLIENTDELEGATION_READ = "altinn:clientdelegations.read";

        /// <summary>
        /// Policy tag for writing client delegation information
        /// </summary>
        public const string POLICY_CLIENTDELEGATION_WRITE = "ClientDelegationWrite";

        /// <summary>
        /// Scope for writing client delegation information
        /// </summary>
        public const string SCOPE_CLIENTDELEGATION_WRITE = "altinn:clientdelegations.write";

        /// <summary>
        /// Represents the AMR claim for ID-Porten self registered email authentication
        /// </summary>
        public const string CLAIM_AMR_IDPORTEN_EMAIL = "Selfregistered-email";

        /// <summary>
        /// Represents the ACR claim for ID-Porten self registered email authentication 
        /// </summary>
        public const string CLAIM_ACR_IDPORTEN_EMAIL = "selfregistered-email";

        /// <summary>
        /// Represents the claim type for the 'idporten-loa-substantial' authentication context class reference (ACR)
        /// used in ID-porten authentication.
        /// </summary>
        public const string CLAIM_ACR_IDPORTEN_SUBSTANTIAL = "idporten-loa-substantial";
    }
}
