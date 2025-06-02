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
    /// Returns partyInfo
    /// </summary>
    /// <param name="orgNo">The OrgNo to lookup</param>
    /// <param name="cancellationToken">The cancellation token<see cref="CancellationToken"/></param>
    /// <returns>party information</returns>
    Task<Party> GetPartyByOrgNo(string orgNo, CancellationToken cancellationToken = default);
}
