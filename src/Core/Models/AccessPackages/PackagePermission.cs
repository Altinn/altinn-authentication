using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Package permissions
    /// </summary>
    public class PackagePermission
    {
        /// <summary>
        /// Package the permissions are for
        /// </summary>
        public CompactPackage Package { get; set; }

        /// <summary>
        /// Parties with permissions
        /// </summary>
        public IEnumerable<Permission> Permissions { get; set; }
    }
}
