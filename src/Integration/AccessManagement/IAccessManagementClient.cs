using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Rights;

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
    /// <param name="token">The authorization header bearer token</param>
    Task<List<DelegationResponseData>?> CheckDelegationAccess(string partyId, DelegationCheckRequest request, string token);

    /// <summary>
    /// Delegates the rights to the systemuser
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <param name="token">The authorization header bearer token</param>
    /// <param name="rights">The Rights to be delegated to the systemuser on behalf of the Party</param>
    /// <param name="systemUser">The SystemUser to receive the rights</param>
    Task<Result<bool>> DelegateRightToSystemUser(string partyId, SystemUser systemUser, List<RightResponses> rights, string token);
}
