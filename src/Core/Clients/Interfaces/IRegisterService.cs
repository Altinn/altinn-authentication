using Altinn.Platform.Authentication.Core.Models;

namespace Altinn.Authentication.Core.Clients.Interfaces;

public interface IRegisterService
{
    Task<(bool Success, PartyInfo? Party)> GetParty(string pid, CancellationToken cancellationToken);
}