#nullable enable

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Settings for the self-identified account-link request flow (issue #2035). The access-management
    /// landing URL is derived from <see cref="GeneralSettings.HostName"/> (see
    /// <c>SelfIdentifiedLinkService</c>), so only the email subject is configured here.
    /// </summary>
    public class SelfIdentifiedLinkSettings
    {
        /// <summary>
        /// Gets or sets the subject of the account-link email. (Localization of subject/body is a
        /// tracked follow-up in #2035.)
        /// </summary>
        public string EmailSubject { get; set; } = "Altinn - kobling av selvidentifisert bruker";
    }
}
