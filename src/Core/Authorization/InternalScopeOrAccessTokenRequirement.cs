#nullable enable
using Altinn.Common.AccessToken;
using Altinn.Common.PEP.Authorization;

namespace Altinn.Platform.Authentication.Core.Authorization;

/// <summary>
/// This requirement was created to allow access if either Scope or AccessToken verification is successful.
/// It inherits from both <see cref="IAccessTokenRequirement"/> and <see cref="IScopeAccessRequirement"/> which
/// will trigger both <see cref="AccessTokenHandler"/> and <see cref="ScopeAccessHandler"/>. If any of them
/// indicate success, authorization will succeed.
/// </summary>
public class InternalScopeOrAccessTokenRequirement : IAccessTokenRequirement, IScopeAccessRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalScopeOrAccessTokenRequirement"/> class with the given scope.
    /// </summary>
    public InternalScopeOrAccessTokenRequirement(string scope)
    {
        ApprovedIssuers = Array.Empty<string>();
        Scope = new string[] { scope };
    }

    /// <inheritdoc/>
    public string[] ApprovedIssuers { get; set; }

    /// <inheritdoc/>
    public string[] Scope { get; set; }
}
