using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Register.Models;

namespace Altinn.Authentication.Tests.Mocks;

/// <summary>
/// Mock class for <see cref="IPartiesClient"></see> interface
/// </summary>
public class PartiesClientMock : IPartiesClient
{
    /// <inheritdoc/>
    public Task<Party> GetPartyAsync(int partyId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetTestDataParties(GetPartiesPath()).Find(p => p.PartyId == partyId));
    }

    private static List<Party> GetTestDataParties(string partiesPath)
    {
        List<Party> partyList = new List<Party>();

        if (File.Exists(partiesPath))
        {
            string content = File.ReadAllText(partiesPath);
            partyList = JsonSerializer.Deserialize<List<Party>>(content);
        }

        return partyList;
    }

    private static string GetPartiesPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, "Data", "Parties", "parties.json");
    }

    private static string GetCustomerPartiesPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, "Data", "Parties", "customerparties.json");
    }

    private static string GetMainUnitsPath(int subunitPartyId)
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, "Data", "MainUnits", $"{subunitPartyId}", "mainunits.json");
    }

    private static string GetKeyRoleUnitsPaths(int userId)
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, "Data", "KeyRoleUnits", $"{userId}", "keyroleunits.json");
    }

    public Task<Organization> GetOrganizationAsync(string partyOrgNo, CancellationToken cancellationToken = default)
    {
        Organization organization = new()
        {
            OrgNumber = partyOrgNo,
        };

        return Task.FromResult<Organization>(organization);
    }

    public Task<Party> GetPartyByOrgNo(string orgNo, CancellationToken cancellationToken = default)
    {
        Party party = new Party();
        party.PartyId = 500000;
        party.PartyUuid = new Guid("00000000-0000-0000-0005-000000000000");

        if (!string.IsNullOrEmpty(orgNo) && orgNo == "987654321")
        {
            party.PartyId = 600000;
            party.PartyUuid = new Guid("6bb78d06-70b2-45f6-85bc-19ca7b4d34d8");
        }

        if (!string.IsNullOrEmpty(orgNo) && orgNo == "123447789")
        {
            party = null;
        }

        return Task.FromResult<Party>(party);
    }
    
    public Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, string accessPackage, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Party> GetPartyByUuId(Guid partyUuId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetTestDataParties(GetCustomerPartiesPath()).Find(p => p.PartyUuid == partyUuId));
    }
}
