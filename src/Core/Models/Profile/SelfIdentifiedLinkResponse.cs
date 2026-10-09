#nullable enable

namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Response for the self-identified account-link email endpoint (issue #2035). Carries the masked
    /// recipient address so the caller can show e.g. "Email sent to r*****@g****.com".
    /// </summary>
    public class SelfIdentifiedLinkResponse
    {
        /// <summary>
        /// Gets or sets the masked email the link was sent to, or <c>null</c> when no email was sent
        /// (unknown/inactive/no-email user, or a delivery failure). The full address is never returned.
        /// </summary>
        public string? MaskedEmail { get; set; }
    }
}
