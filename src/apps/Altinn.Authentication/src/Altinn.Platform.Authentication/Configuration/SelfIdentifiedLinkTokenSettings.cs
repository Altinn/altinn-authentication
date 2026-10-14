#nullable enable

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Settings for the short-lived, single-purpose token that links a self-identified (SI) user
    /// account to an authenticated person in the forgot-password / account-claim flow (issue #2035).
    /// </summary>
    /// <remarks>
    /// This token is <b>not</b> an authentication token and must never be usable as one. It is signed
    /// and validated independently of the OIDC signing certificates (see
    /// <c>ISelfIdentifiedLinkTokenCertificateProvider</c>) and is scoped to the link/claim flow via a
    /// dedicated <see cref="Issuer"/>, <see cref="Audience"/> and purpose claim.
    /// </remarks>
    public class SelfIdentifiedLinkTokenSettings
    {
        /// <summary>
        /// Gets or sets the token issuer (<c>iss</c>). Distinct from the OIDC issuer so the token is
        /// unambiguously scoped to the self-identified link flow.
        /// </summary>
        public string Issuer { get; set; } = "https://platform.altinn.no/authentication/selfidentified-link";

        /// <summary>
        /// Gets or sets the intended audience (<c>aud</c>) - the access-management consumer that
        /// redeems the link.
        /// </summary>
        public string Audience { get; set; } = "altinn:accessmanagement:selfidentified-link";

        /// <summary>
        /// Gets or sets the token lifetime in minutes. Kept short; the link is single-purpose.
        /// </summary>
        public int LifetimeMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the allowed clock skew in seconds when validating the token's lifetime.
        /// </summary>
        public int ClockSkewSeconds { get; set; } = 30;
    }
}
