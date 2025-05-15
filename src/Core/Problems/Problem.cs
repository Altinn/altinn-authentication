using Altinn.Authorization.ProblemDetails;
using System.Net;

namespace Altinn.Authentication.Core.Problems;
/// <summary>
/// Problem descriptors for the Authentication UI BFF.
/// </summary>
public static class Problem
{
    private static readonly ProblemDescriptorFactory _factory
        = ProblemDescriptorFactory.New("AUTH");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Reportee_Orgno_NotFound{ get; }
        = _factory.Create(0, HttpStatusCode.BadRequest, "Can't resolve the Organisation Number from the logged in Reportee PartyId.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Rights_NotFound_Or_NotDelegable { get; }
        = _factory.Create(1, HttpStatusCode.BadRequest, "One or more Right not found or not delegable.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Rights_FailedToDelegate { get; }
        = _factory.Create(2, HttpStatusCode.BadRequest, "The Delegation failed.");


    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor SystemUser_FailedToCreate { get; }
        = _factory.Create(3, HttpStatusCode.BadRequest, "Failed to create the SystemUser.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor SystemUser_AlreadyExists { get; }
        = _factory.Create(4, HttpStatusCode.BadRequest, "Failed to create new SystemUser, existing SystemUser tied to the given System-Id.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Generic_EndOfMethod { get; }
        = _factory.Create(5, HttpStatusCode.BadRequest, "Default error at the end of logic chain. Not supposed to appear.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor ExternalRequestIdAlreadyAccepted { get; }
        = _factory.Create(6, HttpStatusCode.BadRequest, "The combination of External Ids refer to an already Accepted SystemUser.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor ExternalRequestIdPending { get; }
        = _factory.Create(7, HttpStatusCode.BadRequest, "The combination of External Ids refer to a Pending Request, please reuse or delete.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor ExternalRequestIdDenied { get; }
        = _factory.Create(8, HttpStatusCode.BadRequest, "The combination of External Ids refer to a Denied Request, please delete and renew the Request.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor ExternalRequestIdRejected { get; }
        = _factory.Create(9, HttpStatusCode.BadRequest, "The combination of External Ids refer to a Rejected Request, please delete and renew the Request.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor RequestNotFound { get; }
        = _factory.Create(10, HttpStatusCode.NotFound, "The Id does not refer to a Request in our system.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor SystemIdNotFound { get; }
        = _factory.Create(11, HttpStatusCode.NotFound, "The Id does not refer to a Registered System.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor RequestCouldNotBeStored { get; }
        = _factory.Create(12, HttpStatusCode.NotFound, "An error occured when storing the Request.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor RequestStatusNotNew { get; }
        = _factory.Create(13, HttpStatusCode.Conflict, "The Status of the Request is not New.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor UnableToDoDelegationCheck { get; }
        = _factory.Create(14, HttpStatusCode.Forbidden, "DelegationCheck failed with unknown error.");
    
     /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor SystemUserNotFound { get; }
        = _factory.Create(15, HttpStatusCode.NotFound, "The SystemUser was not found.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor DelegationRightMissingRoleAccess { get; }
        = _factory.Create(16, HttpStatusCode.Forbidden, "DelegationCheck failed with error: Has not access by a delegation of role in ER or Altinn.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>        
    public static ProblemDescriptor SystemNameNotFound { get; }
        = _factory.Create(17, HttpStatusCode.NotFound, "The SystemName was not found.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor DelegationRightMissingDelegationAccess { get; }
        = _factory.Create(18, HttpStatusCode.Forbidden, "DelegationCheck failed with error: Has not access by direct delegation.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor DelegationRightMissingSrrRightAccess { get; }
        = _factory.Create(19, HttpStatusCode.Forbidden, "DelegationCheck failed with error: The service requires explicit access in SRR and the reportee is missing this.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor DelegationRightInsufficientAuthenticationLevel { get; }
        = _factory.Create(20, HttpStatusCode.Forbidden, "DelegationCheck failed with error: The service requires explicit authentication level and the reportee is missing this.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>    
    public static ProblemDescriptor RedirectUriNotFound { get; }
        = _factory.Create(21, HttpStatusCode.BadRequest, "The RedirectUri was not found or not valid.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Rights_FailedToRevoke { get; }
        = _factory.Create(22, HttpStatusCode.BadRequest, "The operation to revoke rights failed.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Vendor_Orgno_NotFound { get; }
        = _factory.Create(23, HttpStatusCode.Unauthorized, "Can't resolve the Organisation Number for the Vendor from the Token.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor Delete_SystemUser_NotOwned { get; }
        = _factory.Create(24, HttpStatusCode.Forbidden, "you don't have rights to delete this systemuser");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor NotAnAgentRequest { get; }
        = _factory.Create(25, HttpStatusCode.BadRequest, "The request id is valid but its not a valid request for creating an agent system user");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>. 
    /// </summary>    
    public static ProblemDescriptor NoRedirectUrisFoundOnSystem { get; }
        = _factory.Create(26, HttpStatusCode.BadRequest, "No redirect uris are set for the system");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor RoleNotFoundForPackage { get; }
        = _factory.Create(27, HttpStatusCode.BadRequest, "The accesspackage provided in the request can't be mapped to a valid role.");


    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor CustomerIdNotFound { get; }
        = _factory.Create(28, HttpStatusCode.BadRequest, "The customer id was not provided or did not validate.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>  
    public static ProblemDescriptor AgentRequestNotFound { get; }
        = _factory.Create(30, HttpStatusCode.NotFound, "The Id does not refer to an AgentRequest in our system.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor CustomerDelegation_FailedToRevoke { get; }
        = _factory.Create(31, HttpStatusCode.BadRequest, "Party does not match delegation facilitator.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_AssignmentNotFound { get; }
        = _factory.Create(32, HttpStatusCode.BadRequest, "Assignment not found.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_FailedToDeleteAgent { get; }
        = _factory.Create(33, HttpStatusCode.BadRequest, "Failed to delete the agent system user");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_ExpectedAgentUserType { get; }
        = _factory.Create(34, HttpStatusCode.BadRequest, "The system user type of the provided system user GUID is not an agent.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_HasDelegations { get; }
        = _factory.Create(35, HttpStatusCode.BadRequest, "The system user has delegations from customer. Remove the delegations before deleting the system user");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_ExpectedStandardUserType { get; }
        = _factory.Create(36, HttpStatusCode.BadRequest, "The system user type of the provided system user GUID is not standard.");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_TooManyAssignments { get; }
        = _factory.Create(37, HttpStatusCode.BadRequest, "To many assignment found");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_DelegationNotFound { get; }
        = _factory.Create(38, HttpStatusCode.BadRequest, "Delegation not found");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_InvalidDelegationFacilitator { get; }
        = _factory.Create(39, HttpStatusCode.BadRequest, "Party does not match delegation facilitator");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_DeleteDelegation_PartyMismatch { get; }
        = _factory.Create(40, HttpStatusCode.BadRequest, "Party does not match delegation assignments");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor PartyId_Request_Mismatch { get; }
        = _factory.Create(41, HttpStatusCode.Forbidden, "Party does not match request's orgno");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor PartyId_AgentRequest_Mismatch { get; }
        = _factory.Create(42, HttpStatusCode.Forbidden, "Party does not match agent request's orgno");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_FailedToGetClients_Unauthorized { get; }
        = _factory.Create(43, HttpStatusCode.Unauthorized, "Unauthorized");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_FailedToGetClients_Forbidden { get; }
        = _factory.Create(43, HttpStatusCode.Forbidden, "Forbidden");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/>.
    /// </summary>
    public static ProblemDescriptor AgentSystemUser_FailedToGetClients { get; }
        = _factory.Create(43, HttpStatusCode.BadRequest, "Failed to get clients");

}