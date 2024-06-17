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
        /// Scope for system user admin access
        /// </summary>
        public const string SCOPE_SYSTEMREGISTER_ADMIN = "altinn:authentication/systemregister.admin";

        /// <summary>
        /// Claim for scopes from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_SCOPE = "scope";

        /// <summary>
        /// Claim for consumer prefixes from maskinporten token
        /// </summary>
        public const string CLAIM_MASKINPORTEN_CONSUMER_PREFIX = "consumer_prefix";
    }

}
