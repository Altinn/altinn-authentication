#nullable enable
using System;

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Settings for the self-identified account-link request flow (issue #2035): where the emailed
    /// link points and the email subject.
    /// </summary>
    public class SelfIdentifiedLinkSettings
    {
        /// <summary>
        /// Gets or sets the access-management frontend URL that consumes the link token. The token is
        /// appended as a <c>token</c> query parameter. Required for the email flow; no default.
        /// </summary>
        public Uri? AccessManagementLinkUrl { get; set; }

        /// <summary>
        /// Gets or sets the subject of the account-link email. (Localization of subject/body is a
        /// tracked follow-up in #2035.)
        /// </summary>
        public string EmailSubject { get; set; } = "Altinn - kobling av selvidentifisert bruker";
    }
}
