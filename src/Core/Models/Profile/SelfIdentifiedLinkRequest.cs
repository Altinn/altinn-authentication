#nullable enable

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Request body for the self-identified account-link email endpoint (issue #2035). The
    /// authenticated person supplies the username of the self-identified account they want to claim.
    /// </summary>
    public class SelfIdentifiedLinkRequest
    {
        /// <summary>
        /// Gets or sets the self-identified user's username (login key).
        /// </summary>
        public string UserName { get; set; } = string.Empty;
    }
}
