using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Permission
    /// </summary>
    public class Permission
    {
        /// <summary>
        /// From party. Always present on a permission returned by Access Management.
        /// </summary>
        public CompactEntity From { get; set; } = null!;

        /// <summary>
        /// To party. Always present on a permission returned by Access Management.
        /// </summary>
        public CompactEntity To { get; set; } = null!;

        /// <summary>
        /// Via party. Null unless the access is inherited through a keyrole party —
        /// Access Management sends "via": null for a direct permission.
        /// </summary>
        public CompactEntity? Via { get; set; }

        /// <summary>
        /// Role. Always present — it is the role the permission is granted through.
        /// </summary>
        public CompactRole Role { get; set; } = null!;

        /// <summary>
        /// Via role. Null unless the access is inherited — Access Management sends
        /// "viaRole": null alongside a null "via".
        /// </summary>
        public CompactRole? ViaRole { get; set; }
    }
}
