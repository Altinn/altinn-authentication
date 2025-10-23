using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using System.Runtime.CompilerServices;

namespace Altinn.Platform.Authentication.Integration.AccessManagement;

public interface IAccessManagementClient
{
    /// <summary>
    /// Gets the Party as an AuthorizedPartyExternal object from the reportee list
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <param name="token">The authorization header bearer token</param>
    /// <returns></returns>
    Task<AuthorizedPartyExternal?> GetPartyFromReporteeListIfExists(int partyId, string token);

    /// <summary>
    /// Gets the Party as a PartyExternal object from the partyid
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <param name="token">The authorization header bearer token</param>
    Task<PartyExternal> GetParty(int partyId, string token);

    /// <summary>
    /// Verifies that the rights can be delegated, and gets the correct model to use in the Delegate step
    /// </summary>
    /// <param name="partyId">The party id</param>
    Task<List<DelegationResponseData>?> CheckDelegationAccess(string partyId, DelegationCheckRequest request);

    /// <summary>
    /// Delegates the rights to the systemuser
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <param name="token">The authorization header bearer token</param>
    /// <param name="rights">The Rights to be delegated to the systemuser on behalf of the Party</param>
    /// <param name="systemUser">The SystemUser to receive the rights</param>
    Task<Result<bool>> DelegateRightToSystemUser(string partyId, SystemUser systemUser, List<RightResponses> rights);

    /// <summary>
    /// Revokes the Delegated the rights to the systemuser
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <param name="token">The authorization header bearer token</param>
    /// <param name="rights">The Rights to be revoked for the systemuser on behalf of the Party</param>
    /// <param name="systemUser">The SystemUser that misses the rights</param>
    Task<Result<bool>> RevokeDelegatedRightToSystemUser(string partyId, SystemUser systemUser, List<Right> rights);

    /// <summary>
    /// Delegate a customer to the Agent SystemUser
    /// </summary>    
    /// <param name="systemUser">The Agent SystemUser</param>
    /// <param name="request">Post Body from BFF containing customerId</param>
    /// <param name="userId">Logged in user</param>
    /// <param name="mockCustomerApi">Mock flag for Customer API</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Success or Failure</returns>    
    Task<Result<List<AgentDelegationResponse>>> DelegateCustomerToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, bool mockCustomerApi, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the list of all delegationIds 
    /// </summary>
    /// <param name="systemUserId">The Guid Id for the Agent SystemUser</param>
    /// <param name="facilitator">The Guid Id for the Facilitator</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Result<List<ConnectionDto>>> GetDelegationsForAgent(Guid systemUserId, Guid facilitator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the access package for the given urn value
    /// </summary>
    /// <param name="urnValue">the urn for the package</param>
    /// <returns>package</returns>
    Task<Package> GetAccessPackage(string urnValue);

    /// <summary>
    /// Deletes the customer delegation to the agent systemuser
    /// </summary>
    /// <param name="facilitatorId">The party id of the  user that represents the facilitator for delegation</param>
    /// <param name="delegationId">The delegation id</param>
    Task<Result<bool>> DeleteCustomerDelegationToAgent(Guid facilitatorId, Guid delegationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the assignment of
    /// </summary>
    /// <param name="facilitatorId">The party id of the  user that represents the facilitator for delegation</param>
    /// <param name="assignmentId">The delegation id</param>
    Task<Result<bool>> DeleteSystemUserAssignment(Guid facilitatorId, Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get clients for a facilitator
    /// </summary>
    /// <param name="facilitatorId">The party id of the  user that represents the facilitator for delegation</param>
    /// <param name="packages">Access package URNs</param>
    Task<Result<List<ClientDto>>> GetClientsForFacilitator(Guid facilitatorId, List<string> packages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if the user has the necessary rights to delegate the requested access packages
    /// </summary>
    /// <param name="partyId">the party id of the delegator</param>
    /// <param name="requestedPackages">list of accesspackages to be delegated</param>
    /// <returns></returns>
    IAsyncEnumerable<Result<AccessPackageDto.Check>> CheckDelegationAccessForAccessPackage(Guid partyId, string[] requestedPackages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a System User to the Access Management
    /// </summary>
    /// <param name="partyUuId">the identifier of the party</param>
    /// <param name="systemUser">the system user id</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns>true if the system user is pushed successfully to access management</returns>
    Task<Result<bool>> PushSystemUserToAM(Guid partyUuId, SystemUser systemUser, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a System User as a Right Holder for the Party
    /// </summary>
    /// <param name="partyUuId">the identifier of the party</param>
    /// <param name="systemUserId">the system user id</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> AddSystemUserAsRightHolder(Guid partyUuId, Guid systemUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a System User as a Right Holder for the Party
    /// </summary>
    /// <param name="partyUuId">the identifier of the party</param>
    /// <param name="systemUserId">the system user id</param>
    /// <param name="cascade">to cascade delete delegations</param>
    /// <param name="cancellationToken">the cancellation token</param>
    /// <returns></returns>
    Task<Result<bool>> RemoveSystemUserAsRightHolder(Guid partyUuId, Guid systemUserId, bool cascade, CancellationToken cancellationToken);

    /// <summary>
    /// Delegates access packages to a system user on behalf of a party
    /// </summary>
    /// <param name="partyUuId">the identifier of the party</param>
    /// <param name="systemUserId">the system user id</param>
    /// <param name="urn">the urn of access package to be delegated</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>true if the delegations are successful</returns>
    Task<Result<bool>> DelegateSingleAccessPackageToSystemUser(Guid partyUuId, Guid systemUserId, string urn, CancellationToken cancellationToken);

    /// <summary>
    /// Delegates access packages to a system user on behalf of a party
    /// </summary>
    /// <param name="partyUuId">the identifier of the party</param>
    /// <param name="systemUserId">the system user id</param>
    /// <param name="urn">the urn of access package to be delegated</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>true if the delegations are successful</returns>
    Task<Result<bool>> DeleteSingleAccessPackageFromSystemUser(Guid partyUuId, Guid systemUserId, string urn, CancellationToken cancellationToken);

    /// <summary>
    /// Gets access packages for a system user on behalf of a party
    /// </summary>
    /// <param name="partyUuId"></param>
    /// <param name="systemUserId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<Result<PackagePermission>> GetAccessPackagesForSystemUser(Guid partyUuId, Guid systemUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of all rights delegated to standard user
    /// </summary>
    /// <param name="systemUserId">The Guid Id for the Agent SystemUser</param>
    /// <param name="party">The Guid Id for the party</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Result<List<RightDelegation>>> GetSingleRightDelegationsForStandardUser(Guid systemUserId, int party, CancellationToken cancellationToken = default);
}
