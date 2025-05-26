using Microsoft.AspNetCore.Authorization;

namespace Altinn.Platform.Authentication.Core.Authorization;

/// <summary>
/// Requirement for authorization policies used for accessing apis. https://docs.asp.net/en/latest/security/authorization/policies.html
/// for details about authorization in asp.net core.
/// </summary>
public class EndUserResourceAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets or sets The Action type defined for the policy using this requirement
    /// </summary>
    public string ActionType { get; set; }

    /// <summary>
    /// Gets or sets the resourcId for the resource that authorization should verified for
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the policy should allow access for unauthorized parties
    /// </summary>
    public bool AllowAllowUnauthorizedParty { get; set; }

    /// <summary>
    /// Initializes a new instance of the Altinn.Common.PEP.Authorization.ResourceAccessRequirement class
    /// </summary>
    /// <param name="actionType">The Action type for this requirement</param>
    /// <param name="resourceId">The resource id for the resource authorization is verified for</param>
    /// <param name="allowAllowUnauthorizedParty">A value indicating whether the policy should allow access for unauthorized parties</param>
    public EndUserResourceAccessRequirement(string actionType, string resourceId, bool allowAllowUnauthorizedParty = false)
    {
        ActionType = actionType;
        ResourceId = resourceId;
        AllowAllowUnauthorizedParty = allowAllowUnauthorizedParty;
    }
}
