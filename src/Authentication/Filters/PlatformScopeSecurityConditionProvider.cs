using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Altinn.Authorization.ServiceDefaults.Swashbuckle.Security;
using Altinn.Common.PEP.Authorization;
using Altinn.Swashbuckle.Security;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Documents the scopes carried by authorization requirements that come from the PEP library
    /// rather than from <c>RequireScopeAnyOf</c>.
    /// </summary>
    /// <remarks>
    /// The shared Altinn conventions only recognise <c>IScopeAnyOfAuthorizationRequirement</c>.
    /// This service also has policies built on PEP's <see cref="IScopeAccessRequirement"/> - the
    /// "internal scope or platform access token" requirement - whose scopes would otherwise be
    /// enforced but undocumented. This plugs them into the same scheme through the extension point
    /// the shared package provides, rather than hand-writing security requirements again.
    /// </remarks>
    internal sealed class PlatformScopeSecurityConditionProvider
        : OpenApiAuthorizationRequirementConditionProvider<IScopeAccessRequirement>
    {
        private readonly IOptionsMonitor<AltinnSecurityOptions> _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformScopeSecurityConditionProvider"/> class.
        /// </summary>
        /// <param name="options">The Altinn security options, resolved per document.</param>
        public PlatformScopeSecurityConditionProvider(IOptionsMonitor<AltinnSecurityOptions> options)
        {
            _options = options;
        }

        /// <inheritdoc/>
        protected override IAsyncEnumerable<SecurityRequirementCondition> GetConditionsForAuthorizationRequirement(
            IScopeAccessRequirement requirement,
            OpenApiSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            // Same resolution order the shared providers use, so this lands on whichever scheme
            // name the document is configured with.
            AltinnSecurityOptions options = _options.Get(context.DocumentName);
            AltinnSecurityOptions defaults = _options.CurrentValue;

            bool enabled = options.EnableAltinnOidcScheme
                ?? defaults.EnableAltinnOidcScheme
                ?? AltinnSecurityOptions.DefaultEnableAltinnOidcScheme;

            string? schemeName = options.AltinnOidcSchemeName
                ?? defaults.AltinnOidcSchemeName
                ?? AltinnSecurityOptions.DefaultAltinnOidcSchemeName;

            if (!enabled || string.IsNullOrEmpty(schemeName))
            {
                return AsyncEnumerable.Empty<SecurityRequirementCondition>();
            }

            return requirement.Scope
                .Select(scope => SecurityRequirementCondition.Create(schemeName, scope))
                .ToAsyncEnumerable();
        }
    }
}
