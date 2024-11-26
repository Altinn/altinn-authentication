using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models.Rights;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

/// <summary>
/// Used as a response to the Frontend BFF when attempting to Create and Delegate to a new SystemUser
/// Returns either a new SystemUser or a ProblemInstance
/// </summary>
public class CreateSystemUserResponse
{
    /// <summary>
    /// Set to true if the SystemUser was created    
    /// </summary>
    public bool IsSuccess { get; set; } = false;

    /// <summary>
    /// The SystemUser created if successfull
    /// </summary>
    public SystemUser? SystemUser { get; set; }

    /// <summary>
    /// Set if the SystemUser was not created
    /// </summary>
    public string Problem { get; set; } = string.Empty;
}
