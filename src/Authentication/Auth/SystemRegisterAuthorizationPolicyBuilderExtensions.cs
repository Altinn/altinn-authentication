using System;

using Altinn.Common.PEP.Authorization;
using CommunityToolkit.Diagnostics;

namespace Microsoft.AspNetCore.Authorization;

/// <summary>
/// Extension methods for <see cref="AuthorizationPolicyBuilder"/>.
/// </summary>
public static class SystemRegisterAuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Adds the requirement that the user must have a set of scopes.
    /// </summary>
    /// <param name="builder">The <see cref="AuthorizationPolicyBuilder"/></param>
    /// <param name="scopes">The required scopes</param>
    /// <returns><paramref name="builder"/></returns>
    public static AuthorizationPolicyBuilder RequireScopeAnyOf(this AuthorizationPolicyBuilder builder, params string[] scopes)
    {
        Guard.IsNotNull(scopes);
        Guard.IsNotEmpty(scopes);

        builder.AddRequirements(new ScopeAccessRequirement(scopes));
        return builder;
    }
}