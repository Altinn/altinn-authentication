using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// The reference.
    /// </summary>
    /// <remarks>
    /// Every observed authorizationReference entry carries both members, hence <c>null!</c>
    /// rather than nullable - nothing in this service reads either of them.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class AuthorizationReferenceAttribute
    {
        /// <summary>
        /// The key for authorization reference. Used for authorization api related to resource
        /// </summary>
        public string Id { get; set; } = null!;

        /// <summary>
        /// The value for authorization reference. Used for authorization api related to resource
        /// </summary>
        public string Value { get; set; } = null!;
    }
}
