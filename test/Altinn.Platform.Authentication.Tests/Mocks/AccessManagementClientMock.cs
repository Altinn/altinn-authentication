using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
using Altinn.Platform.Authentication.Core.Models.Pagination;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Register.Contracts.V1;
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

    public async Task<Result<bool>> DelegateRightToSystemUser(Guid partyId, SystemUserInternalDTO systemUser, List<RightResponses> rights)
    {
        if (partyId == Guid.Parse("c48fc8fc-3695-40d5-90d1-fd12cb51075b"))
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

    public Task<bool> CreateSelfIdentifiedUserConnection(Guid from, Guid to, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<AuthorizedPartyExternal> GetPartyFromReporteeListIfExists(int partyId, string token)
    {
        return new AuthorizedPartyExternal();
    }

    public async Task<Result<bool>> RevokeDelegatedRightToSystemUser(Guid partyId, SystemUserInternalDTO systemUser, List<Right> rights)
    {
        return await Task.FromResult(true);
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
   
    public async IAsyncEnumerable<Result<AccessPackageDto.Check>> CheckDelegationAccessForAccessPackage(Guid partyId, string[] requestedPackages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string dataFileName = string.Empty;
        if (partyId == new Guid("39c4f60a-d432-4672-820d-2825c4a0d881"))
        {
            dataFileName = "Data/Delegation/CheckDelegationAccessPackageResponse_NotDelegable.json";
        }
        else if (partyId == new Guid("7a851ad6-3255-4c9b-a727-0b449797eb09"))
        {
            ProblemInstance problemInstance = ProblemInstance.Create(Problem.AccessPackage_DelegationCheckFailed);
            yield return new Result<AccessPackageDto.Check>(problemInstance);
        }
        else
        {
            dataFileName = "Data/Delegation/CheckDelegationAccessPackageResponse.json";
        }

        string content = File.ReadAllText(dataFileName);
        PaginatedInput<AccessPackageDto.Check> paginatedAccessPackages = JsonSerializer.Deserialize<PaginatedInput<AccessPackageDto.Check>>(content, _serializerOptions)!;

        // List<AccessPackageDto.Check> accessPackages = JsonSerializer.Deserialize<List<AccessPackageDto.Check>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (AccessPackageDto.Check accessPackageCheck in paginatedAccessPackages.Items)
        {
            yield return accessPackageCheck;
        }
    }

    public async Task<Result<bool>> PushSystemUserToAM(Guid partyUuId, SystemUserInternalDTO systemUser, CancellationToken cancellationToken)
    {
        return true;
    }

    public async Task<Result<bool>> DelegateSingleAccessPackageToSystemUser(Guid partyUuId, Guid systemUserId, string urn, CancellationToken cancellationToken)
    {
        return true;
    }

    public async Task<Result<bool>> RemoveSystemUserAsRightHolder(Guid partyUuId, Guid systemUserId, bool cascade, CancellationToken cancellationToken)
    {
        if (partyUuId == new Guid("39c4f60a-d432-4672-820d-2825c4a0d881"))
        {
            return false;
        }
        else if (partyUuId == new Guid("c8987f17-a1b5-49f3-8ec5-7b58e2e33f42"))
        {
            ProblemInstance problemInstance = ProblemInstance.Create(Problem.SystemUser_FailedToRemoveRightHolder);
            return new Result<bool>(problemInstance);
        }
        else
        {
            return true;
        }            
    }

    public async Task<Result<bool>> DeleteSingleAccessPackageFromSystemUser(Guid partyUuId, Guid systemUserId, string urn, CancellationToken cancellationToken)
    {
        return true;
    }

    public async IAsyncEnumerable<Result<PackagePermission>> GetAccessPackagesForSystemUser(Guid partyUuId, Guid systemUserId, CancellationToken cancellationToken)
    {
        string dataFileName = string.Empty;
        if (partyUuId == new Guid("39c4f60a-d432-4672-820d-2825c4a0d881"))
        {
            dataFileName = "Data/Delegation/AccessPackagesForSystemUser.json";
        }        
        else if (partyUuId == new Guid("c8987f17-a1b5-49f3-8ec5-7b58e2e33f43"))
        {
            ProblemInstance problemInstance = ProblemInstance.Create(Problem.AccessPackage_DelegationCheckFailed);
            yield return new Result<PackagePermission>(problemInstance);
            yield break;
        }       
        else
        {
            dataFileName = "Data/Delegation/AccessPackagesForSystemUser.json";
        }

        string content = File.ReadAllText(dataFileName);
        PaginatedInput<PackagePermission> paginatedAccessPackagesForSystemUser = JsonSerializer.Deserialize<PaginatedInput<PackagePermission>>(content, _serializerOptions)!;

        foreach (PackagePermission packagePermission in paginatedAccessPackagesForSystemUser.Items)
        {
            yield return packagePermission;
        }
    }

    public async Task<Result<List<RightDelegation>>> GetSingleRightDelegationsForStandardUser(Guid systemUserId, Guid party, CancellationToken cancellationToken = default)
    {
        string dataFileName = string.Empty;
        if (systemUserId == new Guid("ec6831bc-379c-469a-8e41-d37d398772c9"))
        {
            dataFileName = "Data/Delegation/RightsForSystemUser.json";
        }
        else if (party == new Guid("00000000-0000-0000-0005-000000000000") || party == new Guid("c8987f17-a1b5-49f3-8ec5-7b58e2e33f42"))
        {
            ProblemInstance problemInstance = ProblemInstance.Create(Problem.SystemUser_FailedToGetDelegatedRights);
            return new Result<List<RightDelegation>>(problemInstance);
        }
        else
        {
            dataFileName = "Data/Delegation/RightsForSystemUser.json";
        }

        string content = File.ReadAllText(dataFileName);
        List<RightDelegation> delegatedRights = JsonSerializer.Deserialize<List<RightDelegation>>(content, _serializerOptions)!;
        return new Result<List<RightDelegation>>(delegatedRights);
    }

    public async Task<ResourceCheckDto?> CheckDelegationAccess(Guid partyId, string resource, CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Parse("2c022f99-b975-48fb-8c74-9c1ef579b515"))
        {
            return null;
        }

        return new() 
        { 
            Resource = new ResourceDto() 
            { 
                Id = Guid.NewGuid(),
            }, 
            Rights = 
            [
                new RightCheckDto() 
                { 
                    Right = new()
                    {
                        Key = "right1"
                    }, 
                    Result = true 
                }, 
                new RightCheckDto() 
                { 
                    Right = new()
                    {
                        Key = "right2"
                    },
                    Result = false 
                }
            ]        
        };
    }

    public Task<Result<List<DelegationDto>>> DelegateCustomerToAgentSystemUser(Guid systemUserId, DelegationBatchInputDto batch, Guid provider, Guid client, CancellationToken cancellationToken)
    {
        List<DelegationDto> delegations =
        [
            new DelegationDto
            {
                FromId = client,
                ToId = systemUserId,
                Changed = true
            }
        ];

        return Task.FromResult<Result<List<DelegationDto>>>(delegations);
    }

    public Task<Result<List<ClientDelegationDto>>> GetClientsForFacilitator(Guid facilitatorId, List<string> packages, CancellationToken cancellationToken)
    {
        if (facilitatorId.ToString() == "6bb78d06-70b2-45f6-85bc-19ca7b4d34d8")
        {
            return Task.FromResult<Result<List<ClientDelegationDto>>>(new List<ClientDelegationDto>());
        }

        if (facilitatorId.ToString() == "ca00ce4a-c30c-4cf7-9523-a65cd3a40232")
        {
            return Task.FromResult<Result<List<ClientDelegationDto>>>(Problem.AgentSystemUser_FailedToGetClients_Forbidden);
        }

        if (facilitatorId.ToString() == "7bb78d06-70b2-45f6-85bc-19ca7b4d34d8")
        {
            return Task.FromResult<Result<List<ClientDelegationDto>>>(Problem.AgentSystemUser_FailedToGetClients_Forbidden);
        }

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        // The data file mirrors the paginated response from Access Management's GetClients
        // endpoint ({ "links": {...}, "data": [ ... ] }), so deserialize the wrapper and take Data.
        string clientData = File.OpenText("Data/Customers/systemusercustomerlist.json").ReadToEnd();
        PaginatedResult<List<ClientDelegationDto>>? paginated = JsonSerializer.Deserialize<PaginatedResult<List<ClientDelegationDto>>>(clientData, options);
        List<ClientDelegationDto> clients = paginated?.Data ?? [];

        if (packages != null && packages.Count > 0)
        {
            // The real API accepts either the full URN (urn:altinn:accesspackage:regnskapsforer-lonn)
            // or the short identifier (regnskapsforer-lonn), so match a package against both forms.
            var packageSet = new HashSet<string>(packages, StringComparer.OrdinalIgnoreCase);
            clients = clients
                .Where(c =>
                    c.Access != null &&
                    c.Access.Any(a =>
                        a.Packages != null &&
                        a.Packages.Any(p =>
                            p.Urn != null &&
                            (packageSet.Contains(p.Urn) || packageSet.Contains(p.Urn.Split(':').Last())))))
                .ToList();
        }

        return Task.FromResult<Result<List<ClientDelegationDto>>>(clients);
    }

    public async Task<Result<bool>> RevokeSystemUserAsAgent(Guid partyUuid, Guid systemuser, bool cascade = false, CancellationToken cancellationToken = default)
    {
        return true;
    }

    public async Task<Result<bool>> RevokeClientFromAgentSystemUser(Guid provider, Guid client, Guid systemuser, CancellationToken cancellationToken)
    {
        if (client == Guid.Parse("024a0fdd-294c-45ce-9a12-262b11983f2d"))
        {
            return Problem.CustomerDelegation_FailedToRevoke;
        }

        return true;
    }

    public async Task<Result<List<ClientDelegationDto>>> GetClientDelegationsForAgent(Guid systemUserId, Guid provider, CancellationToken cancellationToken = default)
    {
        // Simulate Unauthorized
        if (provider == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"))
        {
            return Problem.AgentSystemUser_FailedToGetClients_Unauthorized;
        }

        // Simulate Forbidden
        if (provider == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"))
        {
            return Problem.AgentSystemUser_FailedToGetClients_Forbidden;
        }

        // Simulate generic failure
        if (provider == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"))
        {
            ProblemInstance problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToGetClients);
            return new Result<List<ClientDelegationDto>>(problemInstance);
        }

        // Simulate empty result
        if (systemUserId == Guid.Parse("fd9d93c7-1dd7-45bc-9772-6ba977b3cd36"))
        {
            return new List<ClientDelegationDto>();
        }

        List<ClientDelegationDto> clientDelegations =
        [
            new()
        {
            Client = new CompactEntityDto()
            {
                Id = new Guid("fd9d93c7-1dd7-45bc-9772-6ba977b3cd36"),
                Name = "Testkunde AS",
                OrganizationIdentifier = "313872076"
            },
            Access =
            [
                new()
                {
                    Role = new CompactRoleDto()
                    {
                        Id = Guid.NewGuid(),
                        Urn = "skatt"
                    },
                    Packages =
                    [
                        new CompactPackageDto()
                        {
                            Id = Guid.NewGuid(),
                            Urn = "urn:altinn:accesspackage:forretningsforer-eiendom"
                        }
                    ]
                }
            ]
        }
        ];

        return clientDelegations;
    }

    private static List<ClientDelegationDto> GetMockClientDelegations()
    {
        return
        [
            new() 
            {
                Client = new CompactEntityDto
                {
                    Id = Guid.Parse("024a0fdd-294c-45ce-9a12-262b11983f2d"),
                    Name = "SPRUDLENDE KRY TIGER AS",
                    Type = "Organisasjon",
                    Variant = "AS",
                    PartyId = 51423145,
                    OrganizationIdentifier = "312403072",
                    IsDeleted = false
                },
                Access =
                [
                    new() 
                    {
                        Role = new CompactRoleDto
                        {
                            Id = Guid.Parse("46e27685-b3ba-423e-8b42-faab54de5817"),
                            Code = "regnskapsforer",
                            Urn = "urn:altinn:external-role:ccr:regnskapsforer",
                            LegacyUrn = "urn:altinn:rolecode:regn"
                        },
                        Packages = [
                        
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("a5f7f72a-9b89-445d-85bb-06f678a3d4d1"),
                                Urn = "urn:altinn:accesspackage:skatt-naering",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("43becc6a-8c6c-4e9e-bb2f-08fe588ada21"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-lonn",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            }
                        ]
                    },
                    new()
                    {
                        Role = new CompactRoleDto
                        {
                            Id = Guid.Parse("46e27685-b3ba-423e-8b42-faab54de5817"),
                            Code = "regnskapsforer",
                            Urn = "urn:altinn:external-role:ccr:regnskapsforer",
                            LegacyUrn = "urn:altinn:rolecode:regn"
                        },
                        Packages = [

                            new CompactPackageDto
                            {
                                Id = Guid.Parse("a5f7f72a-9b89-445d-85bb-06f678a3d4d1"),
                                Urn = "urn:altinn:accesspackage:skatt-naerfefing",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("43becc6a-8c6c-4e9e-bb2f-08fe588ada21"),
                                Urn = "urn:altinn:accesspackage:regnskapsferorer-lonn",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("955d5779-3e2b-4098-b11d-0431dc41ddbe"),
                                Urn = "urn:altinn:accesspackage:forretningsforer-eiendom",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            }
                        ]
                    }
                ]
            },
            new ClientDelegationDto
            {
                Client = new CompactEntityDto
                {
                    Id = Guid.Parse("035aa3a6-014a-4e58-ad05-4a1c586496f0"),
                    Name = "SKY OPPRIKTIG TIGER AS",
                    Type = "Organisasjon",
                    Variant = "AS",
                    PartyId = 51466571,
                    OrganizationIdentifier = "312858762",
                    IsDeleted = false
                },
                Access =
                [
                    new()
                    {
                        Role = new CompactRoleDto
                        {
                            Id = Guid.Parse("46e27685-b3ba-423e-8b42-faab54de5817"),
                            Code = "regnskapsforer",
                            Urn = "urn:altinn:external-role:ccr:regnskapsforer",
                            LegacyUrn = "urn:altinn:rolecode:regn"
                        },
                        Packages =
                        [
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("43becc6a-8c6c-4e9e-bb2f-08fe588ada21"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-lonn",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("955d5779-3e2b-4098-b11d-0431dc41ddbe"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("a5f7f72a-9b89-445d-85bb-06f678a3d4d1"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            }
                        ]
                    }
                ]                
            },
            new ClientDelegationDto
            {
                Client = new CompactEntityDto
                {
                    Id = Guid.Parse("06160751-53c0-4dcd-bbb9-0c54f5d230df"),
                    Name = "VIKTIG MORSOM KROKODILLE",
                    Type = "Organisasjon",
                    Variant = "STI",
                    PartyId = 51497959,
                    OrganizationIdentifier = "313920879",
                    IsDeleted = false
                },
                Access =
                [
                    new() 
                    {
                        Role = new CompactRoleDto
                        {
                            Id = Guid.Parse("46e27685-b3ba-423e-8b42-faab54de5817"),
                            Code = "regnskapsforer",
                            Urn = "urn:altinn:external-role:ccr:regnskapsforer",
                            LegacyUrn = "urn:altinn:rolecode:regn"
                        },
                        Packages =
                        [
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("a5f7f72a-9b89-445d-85bb-06f678a3d4d1"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("955d5779-3e2b-4098-b11d-0431dc41ddbe"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("43becc6a-8c6c-4e9e-bb2f-08fe588ada21"),
                                Urn = "urn:altinn:accesspackage:regnskapsforer-lonn",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            },
                            new CompactPackageDto
                            {
                                Id = Guid.Parse("43becc6a-8c6c-4e9e-bb2f-08fe588ada22"),
                                Urn = "urn:altinn:accesspackage:forretningsforer-eiendom",
                                AreaId = Guid.Parse("64cbcdc8-01c9-448c-b3d2-eb9582beb3c2")
                            }

                        ]
                    }
                ]
            }
        ];
    }
}
