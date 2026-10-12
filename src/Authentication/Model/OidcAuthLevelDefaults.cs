using System.Collections.Generic;
using Altinn.Platform.Authentication.Enum;

#nullable enable

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// The level table applied to any provider that does not declare its own
    /// <see cref="OidcProvider.AuthLevels"/>.
    /// </summary>
    /// <remarks>
    /// This is ID-porten's vocabulary, which used to be hardcoded across
    /// <c>AuthenticationHelper</c>, <c>AuthorizeRequestValidator</c> and <c>OidcServerService</c>.
    /// Keeping it as the default means existing provider configuration (ID-porten, UIDP, the test
    /// providers) needs no change and behaves exactly as before.
    /// <c>level0</c>-<c>level2</c> are legacy and deprecated, but still accepted.
    /// </remarks>
    public static class OidcAuthLevelDefaults
    {
        /// <summary>
        /// ID-porten's levels, as Altinn-facing acr values.
        /// </summary>
        public static readonly OidcAuthLevel[] IdPorten =
        [
            new() { Acr = "selfregistered-email", Level = SecurityLevel.SelfIdentifed, UpstreamAcrValues = "selfregistered-email", ClaimValues = ["selfregistered-email", "idporten-loa-low"] },
            new() { Acr = "level0", Level = SecurityLevel.SelfIdentifed, UpstreamAcrValues = "level0", ClaimValues = ["level0"] },
            new() { Acr = "level1", Level = SecurityLevel.NotSensitive, UpstreamAcrValues = "level1", ClaimValues = ["level1"] },
            new() { Acr = "level2", Level = SecurityLevel.QuiteSensitive, UpstreamAcrValues = "level2", ClaimValues = ["level2"] },
            new() { Acr = "idporten-loa-substantial", Level = SecurityLevel.Sensitive, UpstreamAcrValues = "idporten-loa-substantial", ClaimValues = ["idporten-loa-substantial", "level3"] },
            new() { Acr = "idporten-loa-high", Level = SecurityLevel.VerySensitive, UpstreamAcrValues = "idporten-loa-high", ClaimValues = ["idporten-loa-high", "level4"] },
        ];

        /// <summary>
        /// The levels a provider offers: its own when configured, otherwise <see cref="IdPorten"/>.
        /// </summary>
        public static IReadOnlyList<OidcAuthLevel> For(OidcProvider provider)
            => provider.AuthLevels is { Count: > 0 } configured ? configured : IdPorten;
    }
}
