using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Register.Contracts.V1;
using RegisterContracts = Altinn.Register.Contracts;

namespace Altinn.Authentication.Core.Clients.Interfaces;

/// <summary>
/// Interface for a client wrapper for the Register party and organisation lookup API.
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
    /// <returns>party information, or <see langword="null"/> if Register did not return the party</returns>
    Task<Party?> GetPartyAsync(int partyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns partyInfo
    /// </summary>
    /// <param name="orgNo">The OrgNo to lookup</param>
    /// <param name="cancellationToken">The cancellation token<see cref="CancellationToken"/></param>
    /// <returns>party information, or <see langword="null"/> if Register did not return the party</returns>
    Task<Party?> GetPartyByOrgNo(string orgNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns partyInfo
    /// </summary>
    /// <param name="partyUuId">The party uuid to lookup</param>
    /// <param name="cancellationToken">The cancellation token<see cref="CancellationToken"/></param>
    /// <returns>party information, or <see langword="null"/> if Register did not return the party</returns>
    Task<Party?> GetPartyByUuId(Guid partyUuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a person party in Register by national identity number (SSN), returning a minimal
    /// party populated with only its identifiers (<c>PartyId</c>/<c>PartyUuid</c>) and the associated
    /// Altinn user (<c>UserId</c>/<c>UserName</c>).
    /// </summary>
    /// <remarks>
    /// Backed by <c>POST register/api/v2/internal/parties/query</c> with <c>fields=uuid,id,user</c>.
    /// Intended as the Register-based replacement for the SBL Bridge user lookup
    /// (<c>profile/users/</c>) in the ID-porten token exchange. Note the returned party may have an
    /// unset <see cref="RegisterContracts.PartyUser"/> when the person has no associated Altinn user.
    /// </remarks>
    /// <param name="ssn">The person's national identity number (fødselsnummer).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched party, or <see langword="null"/> if the person was not found.</returns>
    Task<RegisterContracts.Party?> GetPartyIdentifiersAndUsernameByPersonIdentifier(string ssn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return all customers of a specific type for party
    /// </summary>
    /// <param name="partyUuid">The party UUID of the party to retrieve customers from</param>
    /// <param name="accessPackage">Access Package</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all party customers</returns>
    Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, string accessPackage, CancellationToken cancellationToken);
}
