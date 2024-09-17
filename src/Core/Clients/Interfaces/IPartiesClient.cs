using Altinn.Platform.Register.Models;

namespace Altinn.Authentication.Core.Clients.Interfaces;

/// <summary>
/// Interface for a client wrapper for integration with SBL bridge delegation request API
/// </summary>
public interface IPartiesClient
{
    /// <summary>
    /// Returns partyInfo
    /// </summary>
    /// <param name="partyId">The party ID to lookup</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/></param>
    /// <returns>party information</returns>
    Task<Party> GetPartyAsync(int partyId, CancellationToken cancellationToken = default);
}
