using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Register.Models;

namespace Altinn.Authentication.Tests.Mocks;

/// <summary>
/// Mock class for <see cref="IPartiesClient"></see> interface
/// </summary>
public class AccessManagementClientMock : IAccessManagementClient
{
    public Task<List<DelegationResponseData>> CheckDelegationAccess(string partyId, DelegationCheckRequest request)
    {
        string dataFileName;
        if (partyId == "500004")
        {
            dataFileName = "Data/Delegation/DelegationAccessResponse_NotDelegable.json";            
        }
        else
        {
            dataFileName = "Data/Delegation/DelegationAccessResponse.json";
        }

        string content = File.ReadAllText(dataFileName);
        return Task.FromResult((List<DelegationResponseData>)JsonSerializer.Deserialize(content, typeof(List<DelegationResponseData>), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    public async Task<Result<bool>> DelegateRightToSystemUser(string partyId, SystemUser systemUser, List<RightResponses> rights)
    {
        if (partyId == "500005")
        {
            return Problem.Rights_FailedToDelegate;
        }
        else
        {
            return await Task.FromResult(true);
        }
    }

    public Task<PartyExternal> GetParty(int partyId, string token)
    {
        throw new NotImplementedException();
    }

    public Task<AuthorizedPartyExternal> GetPartyFromReporteeListIfExists(int partyId, string token)
    {
        throw new NotImplementedException();
    }

    public Task<Result<bool>> RevokeDelegatedRightToSystemUser(string partyId, SystemUser systemUser, List<Right> rights)
    {
        throw new NotImplementedException();
    }
}
