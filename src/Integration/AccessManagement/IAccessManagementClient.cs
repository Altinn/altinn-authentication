using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

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
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Success or Failure</returns>    
    Task<Result<AgentDelegationResponseExternal>> DelegateCustomerToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the list of all delegationIds 
    /// </summary>
    /// <param name="system"></param>
    /// <param name="facilitator"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<List<AgentDelegationResponseExternal>>> GetDelegationsForAgent(Guid system, Guid facilitator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the access package for the given urn value
    /// </summary>
    /// <param name="urnValue">the urn for the package</param>
    /// <returns>package</returns>
    Task<Package> GetAccessPackage(string urnValue);
}
