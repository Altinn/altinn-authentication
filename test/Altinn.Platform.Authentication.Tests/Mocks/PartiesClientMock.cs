﻿using System;
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
        return Task.FromResult(GetTestDataParties().Find(p => p.PartyId == partyId));
    }

    private static List<Party> GetTestDataParties()
    {
        List<Party> partyList = new List<Party>();

        string partiesPath = GetPartiesPath();
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
        Party party = new()
        {
            PartyId = 500000,
            PartyUuid = new Guid("00000000-0000-0000-0005-000000000000")
        };

        return Task.FromResult<Party>(party);
    }
    
    public Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, string accessPackage, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
