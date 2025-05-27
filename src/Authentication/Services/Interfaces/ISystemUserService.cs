using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Services.Interfaces;
#nullable enable
/// <summary>
/// The service that supports the System User CRUD APIcontroller
/// </summary>
public interface ISystemUserService
{
    /// <summary>
    /// Returns the list of Default SystemUsers this PartyID has registered
    /// </summary>
    /// <param name="partyId">The User id for the Legal Entity (Organisation or Person) the Caller represent.</param> 
    /// <returns></returns>
    Task<List<SystemUser>> GetListOfSystemUsersForParty(int partyId);

    /// <summary>
    /// Return a single SystemUser by PartyId and SystemUserId
    /// </summary>
    /// <param name="systemUserId">The db id for the SystemUser to be retrieved</param>
    /// <returns></returns>
    Task<SystemUser?> GetSingleSystemUserById(Guid systemUserId);

    /// <summary>
    /// Set the Delete flag on the identified SystemUser
    /// </summary>
    /// <param name="partyId">the party id of the reportee</param>
    /// <param name="systemUserId">The db id for the SystemUser to be deteled</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> SetDeleteFlagOnSystemUser(string partyId, Guid systemUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new SystemUser
    /// The unique Id for the systemuser is handled by the db.
    /// But the calling client may send a guid for the request of creating a new system user
    /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
    /// </summary>
    /// <param name="party">The partyId for the reportee</param>
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param>
    /// <param name="userId">The userid for the loged in Reporter User.</param>
    /// <returns></returns> 
    Task<Result<SystemUser>> CreateSystemUser(string party, SystemUserRequestDto request, int userId);

    /// <summary>
    /// Replaces the values for the existing system user with those from the update 
    /// </summary>
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param> 
    /// <returns>Returns the number of rows affected</returns>
    Task<int> UpdateSystemUserById(SystemUserUpdateDto request);

    /// <summary>
    /// Used by IdPorten, to find if a given systemOrg owns a SystemUser Integration for a Vendor's Product, by ClientId.
    /// ConsumerId is the first entry in the path.
    /// SystemOrg is the second entry in the path.
    /// ClientId is the third entry in the path.
    /// </summary>
    /// <param name="clientId">The unique id maintained by IdPorten tying their clients to the Registered Systems we maintain</param>        
    /// <param name="systemProviderOrgNo">The legal number (Orgno) of the Vendor creating the Registered System (Accounting system)</param>
    /// <param name="systemUserOwnerOrgNo">The legal number (Orgno) of the party owning the System User Integration</param>
    /// <param name="externalRef">The External Reference is provided by the Vendor, and is used to identify their Customer in the Vendor's system.</param>
    /// <param name="cancellationToken">Cancellationtoken</param>/// 
    /// <returns>The SystemUserIntegration model API DTO</returns>
    Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, string externalRef, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of SystemUsers the Vendor has for a given system they own.
    /// </summary>
    /// <param name="vendorOrgNo">The logged in vendor</param>
    /// <param name="systemId">The system the Vendor wants the list for</param>
    /// <param name="continueRequest">The Guid denoting from where to continue with Pagination</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<Result<Page<SystemUser, string>>> GetAllSystemUsersByVendorSystem(OrganisationNumber vendorOrgNo, string systemId, Page<string>.Request continueRequest, CancellationToken cancellationToken);

    /// <summary>
    /// Called from the Authn.UI BFF to create a new SystemUser and delegate it to the given Reportee Party,
    /// returns either the new SystemUser or the ProblemInstance from the Result
    /// </summary>
    /// <param name="party">the Reportee partyId</param>
    /// <param name="request">the requested SystemUser to be created</param>
    /// <param name="userId">the logged in user</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns>The CreateSystemUserResponse response model, with either a new SystemUser model inside, or a list of errors.</returns>
    Task<Result<SystemUser>> CreateAndDelegateSystemUser(string party, SystemUserRequestDto request, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a SystemUser by the ExternalRequestId    /// 
    /// </summary>
    /// <param name="externalRequestId">External Ref + Orgno + Systemid should uniquely define a SystemUser</param>
    /// <returns>A SystemUser, if one is active.</returns>
    Task<SystemUser?> GetSystemUserByExternalRequestId(ExternalRequestId externalRequestId);

    /// <summary>
    /// Returns a list of all SystemUsers    
    /// </summary>
    /// <returns>List of SystemUser</returns>
    Task<Result<IEnumerable<SystemUserRegisterDTO>>> GetAllSystemUsers(long continueFrom, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the max sequence no for SystemUsers
    /// </summary>
    /// <returns></returns>
    Task<long> GetMaxSystemUserSequenceNo();

    /// <summary>
    /// Returns the list of Agent SystemUsers this PartyID has registered
    /// </summary>
    /// <param name="party">The User id for the Facilitator for the Agent SystemUser currently logged in at the FrontEnd.</param> 
    /// <returns>List of Agent SystemUsers</returns>
    Task<List<SystemUser>?> GetListOfAgentSystemUsersForParty(int party);

    /// <summary>
    /// Creates a new delegation of a customer to an Agent SystemUser.
    /// The service is idempotent.
    /// </summary>
    /// <returns>Result of True or False</returns> 
    Task<Result<List<DelegationResponse>>> DelegateToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a list of the Delegations (of clients) to an Agent SystemUser,
    /// retrieved in turn from the AccessManagement db.
    /// </summary>
    /// <param name="facilitator">the guid id of the logged in user, representing the Facilitator</param>
    /// <param name="systemUserId">The Guid for the Agent SystemUser</param>
    /// <returns>List of Client Delegations</returns>
    Task<Result<List<DelegationResponse>>> GetListOfDelegationsForAgentSystemUser(Guid facilitator, Guid systemUserId);

    /// <summary>
    /// Delete the client delegation to the Agent SystemUser
    /// </summary>
    /// <param name="partyId">the party id of the facilitator</param>
    /// <param name="delegationId">the id of the delegation between customer and agent system user</param>
    /// <param name="facilitatorId">the guid of facilitator</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> DeleteClientDelegationToAgentSystemUser(string partyId, Guid delegationId, Guid facilitatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the agent system user (soft delete)
    /// </summary>
    /// <param name="partyId">the party id of the reportee</param>
    /// <param name="systemUserId">The db id for the SystemUser to be deteled</param>
    /// <param name="facilitatorId">the guid of facilitator</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> DeleteAgentSystemUser(string partyId, Guid systemUserId, Guid facilitatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list of clients available for a facilitator,
    /// </summary>
    /// <param name="facilitator">the guid id of the logged in user, representing the Facilitator</param>
    /// <param name="packages">An array of access package URNs. Only clients associated with at least one of these access packages will be included in the result.</param>
    /// <returns>List of Clients</returns>
    Task<Result<List<Customer>>> GetClientsForFacilitator(Guid facilitator, List<string> packages, CancellationToken cancellationToken = default);
}
