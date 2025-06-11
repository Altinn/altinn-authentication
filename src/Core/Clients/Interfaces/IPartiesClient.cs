using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Register.Models;

namespace Altinn.Authentication.Core.Clients.Interfaces;

/// <summary>
/// Interface for a client wrapper for integration with SBL bridge delegation request API
/// </summary>
public interface IPartiesClient
{
    /// <summary>
    /// Returns an Organisation based on the input Orgno from the Register
    /// </summary>
    /// <param name="partyOrgNo">The org number to verify</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<Organization?> GetOrganizationAsync(string partyOrgNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns partyInfo
    /// </summary>
    /// <param name="partyId">The party ID to lookup</param>
    /// <param name="cancellationToken">The cancellation token<see cref="CancellationToken"/></param>
    /// <returns>party information</returns>
    Task<Party> GetPartyAsync(int partyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return all customers of a specific type for party
    /// </summary>
    /// <param name="partyUuid">The party UUID of the party to retrieve customers from</param>
    /// <param name="accessPackage">Access Package</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all party customers</returns>
    Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, string accessPackage, CancellationToken cancellationToken);
}
