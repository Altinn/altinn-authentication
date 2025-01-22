using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models
{
    /// <summary>
    /// Attribute identifier class
    /// </summary>
    public static class AttributeIdentifier
    {
        /// <summary>
        /// Org attribute match indentifier 
        /// </summary>
        public const string OrgAttribute = "urn:altinn:org";

        /// <summary>
        /// App attribute match indentifier 
        /// </summary>
        public const string AppAttribute = "urn:altinn:app";

        /// <summary>
        /// Resource registry attribute match indentifier 
        /// </summary>
        public const string ResourceRegistryAttribute = "urn:altinn:resource";

    }
}
