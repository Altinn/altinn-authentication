using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Altinn.AccessManagement.Core.Helpers;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Register.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Npgsql.Internal;

namespace Altinn.Authentication.Tests.Mocks;
#nullable enable
/// <summary>
/// Mock class for <see cref="IPartiesClient"></see> interface
/// </summary>
public class AccessManagementClientMock: IAccessManagementClient    
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AccessManagementSettings _accessManagementSettings;
    private readonly PlatformSettings _platformSettings;
    private readonly JsonSerializerOptions _serializerOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly IWebHostEnvironment _env;

    public AccessManagementClientMock(
        HttpClient httpClient,
        ILogger<AccessManagementClient> logger,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AccessManagementSettings> accessManagementSettings,
        IOptions<PlatformSettings> platformSettings,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _accessManagementSettings = accessManagementSettings.Value;
        _platformSettings = platformSettings.Value;
        httpClient.BaseAddress = new Uri(_accessManagementSettings.ApiAccessManagementEndpoint!);
        _client = httpClient;
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
        _env = env;
    }

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

    public async Task<Result<AgentDelegationResponseExternal>> DelegateCustomerToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, CancellationToken cancellationToken)
    {
        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;

        if (token == null) 
        { 
            return Problem.Rights_FailedToDelegate; 
        }

        List<AgentDelegationDetails> delegations = [];

        foreach (var pac in systemUser.AccessPackages)
        {
            AgentDelegationDetails delegation = new()
            {
                ClientRole = GetRoleFromAccessPackage(pac.Urn!) ?? "NOTFOUND",
                AccessPackage = pac.Urn!.ToString()
            };

            if (delegation.ClientRole == "NOTFOUND")
            {
                return Problem.Rights_FailedToDelegate;
            }

            delegations.Add(delegation);
        }

        AgentDelegationRequest agentDelegationRequest = new()
        {
            AgentId = Guid.Parse(systemUser.Id),
            AgentName = systemUser.IntegrationTitle,
            AgentRole = "Agent",
            ClientId = Guid.Parse(request.CustomerId),
            FacilitatorId = Guid.Parse(request.FacilitatorId),
            Delegations = delegations
        };

        string endpointUrl = $"internal/delegation/systemagent";

        return new AgentDelegationResponseExternal();
    }

    /// <summary>
    ///  Only for use in the PILOT test in tt02
    /// </summary>
    /// <param name="accessPackage">The accesspackage requested on the system user</param>
    /// <returns></returns>
    private static string? GetRoleFromAccessPackage(string accessPackage)
    {
        Dictionary<string, string> hardcodingOfAccessPackageToRole = [];

        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet", "REGN");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet", "REGN");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-lonn", "REGN");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:ansvarlig-revisor", "REVI");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:revisormedarbeider", "REVI");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:skattegrunnlag", "FFOR");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:skattnaering", "FFOR");

        hardcodingOfAccessPackageToRole.TryGetValue(accessPackage, out string? found);        
        return found;   
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

    public Task<Package> GetAccessPackage(string urnValue)
    {
        Package? package = null;
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        string packagesData = File.OpenText("Data/Packages/packages.json").ReadToEnd();
        List<Package>? packages = JsonSerializer.Deserialize<List<Package>>(packagesData, options);
        package = packages?.FirstOrDefault(p => p.Urn.Contains(urnValue, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(package);
    }

    public Task<Package> GetPackage(string packageId)
    {
        Package? package = null;
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        string packagesData = File.OpenText("Data/Packages/packages.json").ReadToEnd();
        List<Package>? packages = JsonSerializer.Deserialize<List<Package>>(packagesData, options);
        package = packages?.FirstOrDefault(p => p.Urn.Contains(packageId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(package);
    }

    public Task<PartyExternal> GetParty(int partyId, string token)
    {
        throw new NotImplementedException();
    }

    public Task<AuthorizedPartyExternal> GetPartyFromReporteeListIfExists(int partyId, string token)
    {
        throw new NotImplementedException();
    }

    public async Task<Result<bool>> RevokeDelegatedRightToSystemUser(string partyId, SystemUser systemUser, List<Right> rights)
    {
        return await Task.FromResult(true);
    }

    public async Task<Result<AgentDelegationResponseExternal>> GetDelegationsForAgent(SystemUser systemUser, Guid facilitator, CancellationToken cancellationToken)
    {
        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;

        if (token == null)
        {
            return Problem.Reportee_Orgno_NotFound;
        }
                
        string endpointUrl = $"internal/systemuserclientdelegation?party={facilitator}";

        return new AgentDelegationResponseExternal();
    }

    public async Task<Result<List<AgentDelegationResponseExternal>>> GetDelegationsForAgent(Guid system, Guid facilitator, CancellationToken cancellationToken = default)
    {
        List<AgentDelegationResponseExternal> delegations = [];
        delegations.Add(new AgentDelegationResponseExternal() 
        { 
            From = new DelegationAssignment()
            {
                FromId = Guid.NewGuid(),
                ToId = Guid.NewGuid(),
                Id = Guid.NewGuid(),
                RoleId = Guid.NewGuid(),
            },
            To = new DelegationAssignment()
            {
                FromId = Guid.NewGuid(),
                ToId = Guid.NewGuid(),
                Id = Guid.NewGuid(),
                RoleId = Guid.NewGuid(),
            },
            FacilitatorId = facilitator,
            Id = system
        });

        return delegations;
    }
}
