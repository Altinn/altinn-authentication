#nullable enable

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Request body for redeeming a self-identified account-link token (issue #2035). Carries the
    /// token delivered in the account-link email.
    /// </summary>
    public class SelfIdentifiedLinkTokenRequest
    {
        /// <summary>
        /// Gets or sets the link token from the email.
        /// </summary>
        public string? Token { get; set; }
    }
}
