using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

[ExcludeFromCodeCoverage]
public class AgentDelegationDetails
{
    /// <summary>
    /// REGN, REVI, FFOR
    /// The Role the Client has delegated to the Facilitator, 
    /// providing the AccessPackage,
    /// through which the faciliator now wants to further Delegate
    /// to the Agent SystemUser.
    /// </summary>
    public required string ClientRole { get; set; }

    /// <summary>
    /// The AccessPackage is a child of one or more Roles, 
    /// and contains one or several Rights.    
    /// This field uses the urn notation, such as:
    /// urn:altinn:accesspackage:ansvarlig-revisor
    /// </summary>
    public required string AccessPackage { get; set; } 
}
