namespace Altinn.Platform.Authentication.Core.Models.Rights;

public enum XacmlContextDecision
{
    /// <summary>
    /// “Permit”: the requested access is permitted
    /// </summary>
    Permit,

    /// <summary>
    /// “Deny”: the requested access is denied.
    /// </summary>
    Deny,

    /// <summary>
    /// “Indeterminate”: the PDP is unable to evaluate the requested access.  Reasons for such inability include: missing attributes, network errors while retrieving policies, division by zero during policy evaluation, syntax errors in the decision request or in the policy, etc.
    /// </summary>
    Indeterminate,

    /// <summary>
    ///  “NotApplicable”: the PDP does not have any policy that applies to this decision request.
    /// </summary>
    NotApplicable,
}
