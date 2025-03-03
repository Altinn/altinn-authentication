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
        IOptions<PlatformSettings> platformSettings)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _accessManagementSettings = accessManagementSettings.Value;
        _platformSettings = platformSettings.Value;
        httpClient.BaseAddress = new Uri(_accessManagementSettings.ApiAccessManagementEndpoint!);
        _client = httpClient;
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
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

    public async Task<Package?> GetPackage(string packageId)
    {
        Package? package = null;

        try
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            string packagesData = File.OpenText("../Integration/MockData/packages.json").ReadToEnd();
            List<Package>? packages = JsonSerializer.Deserialize<List<Package>>(packagesData, options);
            package = packages?.FirstOrDefault(p => p.Urn.Contains(packageId, StringComparison.OrdinalIgnoreCase));
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
}
