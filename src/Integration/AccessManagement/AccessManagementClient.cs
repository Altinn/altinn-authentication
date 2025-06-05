using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Platform.Authentication.Core.Models;
using System.Text;
using System.Net.Http.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Extensions;
using Altinn.Authentication.Core.Problems;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Microsoft.Extensions.Primitives;
using Altinn.AccessManagement.Core.Helpers;
using Altinn.Authentication.Integration.Configuration;
using Microsoft.AspNetCore.Mvc;
using Altinn.Platform.Authentication.Core.Exceptions;
using System.Net;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using System.Net.Http;
using System.Web;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Register.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Azure;
using Azure;
using static Altinn.Platform.Authentication.Core.Models.SystemUsers.ClientDto;


namespace Altinn.Platform.Authentication.Integration.AccessManagement;

/// <summary>
/// Proxy implementation for parties
/// </summary>
[ExcludeFromCodeCoverage]
public class AccessManagementClient : IAccessManagementClient
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AccessManagementSettings _accessManagementSettings;
    private readonly PlatformSettings _platformSettings;
    private readonly JsonSerializerOptions _serializerOptions =
        new() { PropertyNameCaseInsensitive = true };
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// Initializes a new instance of the <see cref="LookupClient"/> class
    /// </summary>
    /// <param name="httpClient">HttpClient from default httpclientfactory</param>
    /// <param name="logger">the logger</param>
    /// <param name="httpContextAccessor">handler for http context</param>
    /// <param name="platformSettings">the platform setttings</param>
    public AccessManagementClient(
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

    /// <inheritdoc/>
    public async Task<AuthorizedPartyExternal?> GetPartyFromReporteeListIfExists(int partyId, string token)
    {
        try
        {
            string endpointUrl = $"authorizedparty/{partyId}?includeAltinn2=true";            

            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl);

            if ( response.StatusCode == System.Net.HttpStatusCode.OK )
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AuthorizedPartyExternal>(responseContent, _serializerOptions)!;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // GetPartyFromReporteeListIfExists // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PartyExternal> GetParty(int partyId, string token)
    {
        try
        {
            string endpointUrl = $"authorizedparty/{partyId}?includeAltinn2=true";            

            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PartyExternal>(responseContent, _serializerOptions)!;
            }

            return null!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // GetParty // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<DelegationResponseData>?> CheckDelegationAccess(string partyId, DelegationCheckRequest request)
    {
        try
        {
            string endpointUrl = $"internal/{partyId}/rights/delegation/delegationcheck";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
            string content = JsonSerializer.Serialize(request, _serializerOptions);
            StringContent requestBody = new(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync(token, endpointUrl, requestBody);
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<List<DelegationResponseData>>( await response.Content.ReadAsStringAsync(), _serializerOptions) ;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // CheckDelegationAccess // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DelegateRightToSystemUser(string partyId, SystemUser systemUser, List<RightResponses> responseData)
    {
        foreach (RightResponses rightResponse in responseData)
        {
            Result<RightsDelegationResponseExternal> result = await DelegateSingleRightToSystemUser(partyId, systemUser, rightResponse);

            if (result.IsProblem)
            {
                return new Result<bool>(result.Problem!);
            }

            bool allDelegated = result.Value.RightDelegationResults.All(r => r.Status == DelegationStatusExternal.Delegated);
            if (!allDelegated)
            {
                var notDelegatedDetails = result.Value.RightDelegationResults
                    .Where(r => r.Status != DelegationStatusExternal.Delegated)
                    .Select(r => r.Details)
                    .ToList();

                var problemDetails = new ProblemDetails
                {
                    Title = "Some rights were not delegated",
                    Detail = "Not all rights were successfully delegated.",
                    Extensions = { { "Details", notDelegatedDetails } }
                };

                _logger.LogError("Authentication.UI // AccessManagementClient // DelegateRightToSystemUser // Problem: {Problem}", problemDetails.Detail);
                throw new DelegationException(problemDetails);
            }
        }

        return new Result<bool>(true);
    }

    public async Task<Package?> GetAccessPackage(string urnValue)
    {
        Package package = null;

        try
        {
            string endpointUrl = $"meta/info/accesspackages/package/urn/{HttpUtility.UrlEncode(urnValue)}";

            HttpResponseMessage response = await _client.GetAsync(endpointUrl);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                string content = await response.Content.ReadAsStringAsync();
                package = JsonSerializer.Deserialize<Package>(content, options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // AccessManagementClient // GetPackage // Exception");
            throw;
        }

        return package;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RevokeDelegatedRightToSystemUser(string partyId, SystemUser systemUser, List<Right> rights)
    {
        if (!await RevokeRightsToSystemUser(partyId, systemUser, rights))
        {
            return Problem.Rights_FailedToRevoke;
        };

        return true;
    }

    private async Task<Result<RightsDelegationResponseExternal>> DelegateSingleRightToSystemUser(string partyId, SystemUser systemUser, RightResponses rightResponses)
    {
        List<Right> rights = [];

        foreach (DelegationResponseData inner in rightResponses.ResponseDataSet)
        {
            Right right = new()
            {
                Action = inner.Action,
                Resource = inner.Resource,
            };

            rights.Add(right);
        }

        DelegationRequest rightsDelegationRequest = new()
        {
            To =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:systemuser:uuid",
                    Value = systemUser.Id
                }
            ],

            Rights = rights
        };

        try
        {
            string endpointUrl = $"internal/{partyId}/rights/delegation/offered";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
            HttpResponseMessage response = await _client.PostAsync(token, endpointUrl, JsonContent.Create(rightsDelegationRequest));

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                RightsDelegationResponseExternal result = JsonSerializer.Deserialize<RightsDelegationResponseExternal>(responseContent, _serializerOptions)!;
                return new Result<RightsDelegationResponseExternal>(result);
            }
            else
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                ProblemDetails problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _serializerOptions)!;
                _logger.LogError($"Authentication.UI // AccessManagementClient // DelegateSingleRightToSystemUser // Title: {problemDetails.Title}, Problem: {problemDetails.Detail}");

                ProblemInstance problemInstance = ProblemInstance.Create(Problem.Rights_FailedToDelegate);
                return new Result<RightsDelegationResponseExternal>(problemInstance);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // DelegateSingleRightToSystemUser // Exception");
            throw;
        }

    }

    private async Task<bool> RevokeRightsToSystemUser(string partyId, SystemUser systemUser, List<Right> rights)
    {
        DelegationRequest revokeDelegatedRights = new()
        {
            To =
            [
                new AttributePair()
                {
                    Id = "urn:altinn:systemuser:uuid",
                    Value = systemUser.Id
                }
            ],

            Rights = rights
        };

        try
        {
            string endpointUrl = $"internal/{partyId}/rights/delegation/offered/revoke";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
            HttpResponseMessage response = await _client.PostAsync(token, endpointUrl, JsonContent.Create(revokeDelegatedRights));

            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // RevokeSingleRightToSystemUser // Exception");
            throw;
        }

    }

    /// <inheritdoc />
    public async Task<Result<List<AgentDelegationResponse>>> DelegateCustomerToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, CancellationToken cancellationToken)
    {
        const string AGENT = "agent";

        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;        
        if ( ! Guid.TryParse(request.FacilitatorId, out Guid facilitator))
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        if (!Guid.TryParse(request.CustomerId, out Guid clientId))
        {
            return Problem.CustomerIdNotFound;
        }

        if (!Guid.TryParse(systemUser.Id, out Guid agentSystemUserId))
        {
            return Problem.SystemUserNotFound;
        }

        List<CreateSystemDelegationRolePackageDto> rolePackages = [];

        foreach (var pac in systemUser.AccessPackages) 
        {
            var role = GetRoleFromAccessPackages(pac.Urn!, request.Access);

            if ( role is null )
            {
                return Problem.RoleNotFoundForPackage;
            }

            CreateSystemDelegationRolePackageDto rolePackage = new()
            {
                RoleIdentifier = role,
                PackageUrn = pac.Urn!.ToString()
            };

            rolePackages.Add(rolePackage);
        }        

        AgentDelegationRequest agentDelegationRequest = new()
        {
            AgentId = agentSystemUserId,
            AgentName = systemUser.IntegrationTitle,
            AgentRole = AGENT, 
            ClientId = clientId,
            FacilitatorId = facilitator,
            RolePackages = rolePackages
        };

        try
        {
            string endpointUrl = $"internal/systemuserclientdelegation?party={facilitator}";
            HttpResponseMessage response = await _client.PostAsync(token, endpointUrl, JsonContent.Create(agentDelegationRequest));

            List<AgentDelegationResponse> found = await response.Content.ReadFromJsonAsync<List<AgentDelegationResponse>>(_serializerOptions, cancellationToken) ?? [];

            if (response.IsSuccessStatusCode && found is not null)
            {
                return found;
            }            

            return new Result<List<AgentDelegationResponse>>(Problem.Rights_FailedToDelegate);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // AccessManagementClient // DelegateCustomerToAgentSystemUser // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteCustomerDelegationToAgent(Guid facilitatorId, Guid delegationId,CancellationToken cancellationToken)
    {
        try
        {
            string endpointUrl = $"internal/systemuserclientdelegation/deletedelegation?party={HttpUtility.UrlEncode(facilitatorId.ToString())}&delegationid={HttpUtility.UrlEncode(delegationId.ToString())}";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
            HttpResponseMessage response = await _client.DeleteAsync(token, endpointUrl);
            return await HandleErrors(response);    
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // RevokeDelegatedAccessPackageToSystemUser // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteSystemUserAssignment(Guid facilitatorId, Guid systemUserId, CancellationToken cancellationToken)
    {
        try
        {
            string endpointUrl = $"internal/systemuserclientdelegation/deleteagentassignment?party={HttpUtility.UrlEncode(facilitatorId.ToString())}&agentid={HttpUtility.UrlEncode(systemUserId.ToString())}";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
            HttpResponseMessage response = await _client.DeleteAsync(token, endpointUrl);

            return await HandleErrors(response, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication.UI // AccessManagementClient // RevokeDelegatedAccessPackageToSystemUser // Exception");
            throw;
        }
    }


    /// <summary>
    ///  Gets the role identifier that gives access to the requested access package
    /// </summary>
    /// <param name="accessPackages">The accesspackage requested for a system user on a system</param>
    /// <returns></returns>
    private static string? GetRoleFromAccessPackages(string accessPackage, List<ClientRoleAccessPackages> clientRoleAccessPackages)
    {
        accessPackage = accessPackage?.Split(":")[3]!;
        if (string.IsNullOrEmpty(accessPackage) || clientRoleAccessPackages == null)
        {
            return null;
        }

        foreach (var clientRoleAccessPackage in clientRoleAccessPackages)
        {
            if (clientRoleAccessPackage.Packages != null && clientRoleAccessPackage.Packages.Contains(accessPackage, StringComparer.OrdinalIgnoreCase))
            {
                return clientRoleAccessPackage.Role;
            }
        }

        return null;
    }

    public async Task<Result<List<ConnectionDto>>> GetDelegationsForAgent(Guid systemUserId, Guid facilitator, CancellationToken cancellationToken = default)
    {
        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
        if (facilitator == Guid.Empty)
        {
            return Problem.Reportee_Orgno_NotFound;
        }
        
        if( systemUserId == Guid.Empty)
        {
            return Problem.SystemUserNotFound;
        };

        string endpointUrl = $"internal/systemuserclientdelegation?party={facilitator}&systemuser={systemUserId}";

        try
        {
            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ConnectionDto>>(_serializerOptions, cancellationToken) ?? [];
            }
        
            return Problem.UnableToDoDelegationCheck;

        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Authentication // AccessManagementClient // GetDelegationsForAgent // Exception");
            throw;

        }
    }

    private async Task<Result<bool>> HandleErrors(HttpResponseMessage response, bool isDeleteAgent = false)
    {
        string deleteString = isDeleteAgent ? "DeleteAgentAssignment" : "DeleteDelegation";
        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        else if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Authentication // AccessManagementClient // {deleteString} // BadRequest: {responseContent}");
            var problemExtensionData = ProblemExtensionData.Create(new[]
{
                    new KeyValuePair<string, string>("Problem Detail : ", responseContent)
                });

            ProblemInstance problemInstance;

            if (responseContent.Contains("Assignment not found"))
            {
                problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_AssignmentNotFound, problemExtensionData);
            }
            else if (responseContent.Contains("To many assignment found"))
            {
                problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_TooManyAssignments, problemExtensionData);
            }
            else if (responseContent.Contains("Delegation not found"))
            {
                problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_DelegationNotFound, problemExtensionData);
            }
            else if (responseContent.Contains("Party does not match delegation facilitator"))
            {
                problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_InvalidDelegationFacilitator, problemExtensionData);
            }
            else if (responseContent.Contains("Party does not match delegation assignments"))
            {
                problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_DeleteDelegation_PartyMismatch, problemExtensionData);
            }
            else
            {
                if(isDeleteAgent)
                {
                    problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToDeleteAgent, problemExtensionData);
                }
                else
                {
                    problemInstance = ProblemInstance.Create(Problem.CustomerDelegation_FailedToRevoke, problemExtensionData);
                }                
            }

            return new Result<bool>(problemInstance);
        }
        else
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            ProblemDetails problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _serializerOptions)!;
            _logger.LogError($"Authentication // AccessManagementClient // {deleteString} // Title: {problemDetails.Title}, Problem: {problemDetails.Detail}");

            var problemExtensionData = ProblemExtensionData.Create(new[]
            {
                    new KeyValuePair<string, string>("Problem Detail: ", problemDetails.Detail)
                });

            ProblemInstance problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToDeleteAgent, problemExtensionData);
            return new Result<bool>(problemInstance);
        }
    }

    public async Task<Result<List<ClientDto>>> GetClientsForFacilitator(Guid facilitatorId, List<string> packages, CancellationToken cancellationToken = default)
    {
        string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!)!;
        if (facilitatorId == Guid.Empty)
        {
            return Problem.Reportee_Orgno_NotFound;
        }

        string endpointUrl = $"internal/systemuserclientdelegation/clients?party={facilitatorId}";
        
        if (packages != null && packages.Count > 0)
        {
            foreach (var package in packages)
            {
                endpointUrl = $"{endpointUrl}&packages={package}";
            }
        }

        try
        {
            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ClientDto>>(_serializerOptions, cancellationToken) ?? [];
            }
            else
            {
                if(response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ProblemInstance problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToGetClients_Unauthorized);
                    return new Result<List<ClientDto>>(problemInstance);
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    ProblemInstance problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToGetClients_Forbidden);
                    return new Result<List<ClientDto>>(problemInstance);
                }
                else
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    ProblemDetails problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseContent, _serializerOptions)!;
                    _logger.LogError($"Authentication // AccessManagementClient // GetClientsForFacilitator // Title: {problemDetails.Title}, Problem: {problemDetails.Detail}");
                    var problemExtensionData = ProblemExtensionData.Create(new[]
                    {
                    new KeyValuePair<string, string>("Problem Detail : ", problemDetails.Detail)
                    });
                    ProblemInstance problemInstance = ProblemInstance.Create(Problem.AgentSystemUser_FailedToGetClients, problemExtensionData);
                    return new Result<List<ClientDto>>(problemInstance);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // AccessManagementClient // GetClientsForFacilitator // Exception");
            throw;

        }
    }
}
