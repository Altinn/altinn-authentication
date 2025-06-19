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

    public async Task<Result<List<AgentDelegationResponse>>> DelegateCustomerToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, bool mockCustomerApi, CancellationToken cancellationToken)
    {
        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;

        if (token == null) 
        { 
            return Problem.Rights_FailedToDelegate; 
        }

        List<AgentDelegationResponse> delegationResult = [];

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

        var delegationId = Guid.NewGuid();

        var ext = new AgentDelegationResponse()
        {
            DelegationId = delegationId,
            FromEntityId = Guid.Parse(request.CustomerId)
        };

        delegationResult.Add(ext);

        return delegationResult;
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
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:forretningsforer-eiendom", "forretningsforer");

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

    public async Task<Result<List<ConnectionDto>>> GetDelegationsForAgent(Guid systemUserId, Guid facilitator, CancellationToken cancellationToken = default)
    {
        List<ConnectionDto> delegations = [];
        
        if (facilitator == new Guid("aafe89c4-8315-4dfa-a16b-1b1592f2b651") || facilitator == new Guid("ca00ce4a-c30c-4cf7-9523-a65cd3a40232") || facilitator == new Guid("32153b44-4da9-4793-8b8f-6aa4f7d17d17") || facilitator == new Guid("23478729-1ffa-49c7-a3d0-6e0d08540e9a"))
        {
            return delegations;
        }

        var delegationId = Guid.NewGuid();       

        delegations.Add(new ConnectionDto() 
        { 
            From = new EntityParty()
            {
                Id = Guid.NewGuid(),
            },
            To = new EntityParty()
            {
                Id = Guid.NewGuid()
            },
            Facilitator = new EntityParty() 
            { 
                Id = facilitator 
            },

            Id = delegationId,
            Delegation = new Delegation()
            {
                Id = delegationId,
                FacilitatorId = facilitator,
                FromId = Guid.NewGuid(),// value not from our input
                ToId = Guid.NewGuid() // the Assignment Id
            }
        });

        return delegations;
    }

    public async Task<Result<bool>> DeleteCustomerDelegationToAgent(Guid partyUUId, Guid delegationId, CancellationToken cancellationToken = default)
    {
        switch (partyUUId.ToString())
        {
            case "02ba44dc-d80b-4493-a942-9b355d491da0":
                return Problem.CustomerDelegation_FailedToRevoke;
            case "199912a2-86e1-4c8e-b010-c8c3956535a7":
                return Problem.AgentSystemUser_DelegationNotFound;
            case "cf814a90-1a14-4323-ae8b-72738abaab49":
                return Problem.AgentSystemUser_InvalidDelegationFacilitator;
            case "1765cf28-2554-4f3c-90c6-a269a01f46c8":
                return Problem.AgentSystemUser_DeleteDelegation_PartyMismatch;
            default:
                return await Task.FromResult(true);
        }
    }

    public async Task<Result<bool>> DeleteSystemUserAssignment(Guid partyUUId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        {
            if (partyUUId == new Guid("ca00ce4a-c30c-4cf7-9523-a65cd3a40232"))
            {
                return Problem.AgentSystemUser_FailedToDeleteAgent;
            }
            else if (partyUUId == new Guid("32153b44-4da9-4793-8b8f-6aa4f7d17d17"))
            {
                return Problem.AgentSystemUser_AssignmentNotFound;
            }
            else if (partyUUId == new Guid("23478729-1ffa-49c7-a3d0-6e0d08540e9a"))
            {
                return Problem.AgentSystemUser_TooManyAssignments;
            }
            else
            {
                return await Task.FromResult(true);
            }
        }
    }

    public async Task<Result<List<ClientDto>>> GetClientsForFacilitator(Guid facilitatorId, List<string> packages, CancellationToken cancellationToken = default)
    {
        if (facilitatorId.ToString() == "ca00ce4a-c30c-4cf7-9523-a65cd3a40232")
        {
            return Problem.AgentSystemUser_FailedToGetClients_Forbidden;
        }

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        string clientData = File.OpenText("Data/Customers/customerlist.json").ReadToEnd();
        List<ClientDto>? clients = JsonSerializer.Deserialize<List<ClientDto>>(clientData, options);

        if (packages != null && packages.Count > 0 && clients != null)
        {
            var packageSet = new HashSet<string>(packages, StringComparer.OrdinalIgnoreCase);
            clients = clients
                .Where(c =>
                    c.Access != null &&
                    c.Access.Any(a =>
                        a.Packages != null &&
                        a.Packages.Any(p => packageSet.Contains(p))))
                .ToList();
        }

        return await Task.FromResult(clients);
    }

    public async Task<ConsentRedirectUrl?> GetConsentRequestRedirectUrl(Guid consentId)
    {
        ConsentRedirectUrl redirectUrl = new()
        {
            Url = "https://altinn.no"
        };
        return await Task.FromResult(redirectUrl);
    }
}
