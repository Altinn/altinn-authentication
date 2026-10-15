using Altinn.Platform.Authentication.Enum;

#nullable enable

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// One authentication level offered by an ID-provider, tying together the four things that
    /// used to be hardcoded per level: what Altinn's own clients ask for, what that means as a
    /// normalised level, what we send upstream to request it, and which upstream claim values
    /// come back for it.
    /// </summary>
    /// <remarks>
    /// Keeping these in one object rather than in separate parallel dictionaries is what lets
    /// step-up work for a provider that does not send <c>acr</c>: a requested acr resolves to a
    /// level, and the session's acr resolves to a level, so the two are comparable.
    /// </remarks>
    public class OidcAuthLevel
    {
        /// <summary>
        /// The Altinn-facing acr value. This is what clients put in <c>acr_values</c>, what is
        /// stored on the session, and what is emitted in the <c>acr</c> claim.
        /// </summary>
        public string Acr { get; set; } = string.Empty;

        /// <summary>
        /// The normalised Altinn security level this acr corresponds to. Drives
        /// <c>urn:altinn:authlevel</c> and all step-up comparisons.
        /// </summary>
        public SecurityLevel Level { get; set; }

        /// <summary>
        /// The <c>acr_values</c> string to send to this provider's authorize endpoint when this
        /// level is requested. May hold several space-separated values. When null or empty,
        /// nothing provider-specific is sent for this level.
        /// </summary>
        /// <example>ID-porten: <c>idporten-loa-high</c>. HelseID: <c>Level4</c>.</example>
        public string? UpstreamAcrValues { get; set; }

        /// <summary>
        /// The values the provider's configured authentication-level claim can take for this
        /// level. For ID-porten this is the acr value itself; for HelseID it is the
        /// <c>helseid://claims/identity/security_level</c> values (<c>"4"</c>, <c>"3"</c>, …).
        /// Matched case-insensitively.
        /// </summary>
        public string[] ClaimValues { get; set; } = [];
    }
}
