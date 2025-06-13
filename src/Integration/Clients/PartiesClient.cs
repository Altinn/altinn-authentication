using Altinn.AccessManagement.Core.Helpers;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Register.Models;
using Altinn.Platform.Authentication.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Text;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Authorization.ProblemDetails;

namespace Altinn.Authentication.Integration.Clients;

/// <summary>
/// Proxy implementation for parties
/// </summary>
[ExcludeFromCodeCoverage]
public class PartiesClient : IPartiesClient
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformSettings _platformSettings;
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="PartiesClient"/> class
    /// </summary>
    /// <param name="httpClient">HttpClient from default httpclientfactory</param>
    /// <param name="sblBridgeSettings">the sbl bridge settings</param>
    /// <param name="logger">the logger</param>
    /// <param name="httpContextAccessor">handler for http context</param>
    /// <param name="platformSettings">the platform setttings</param>
    /// <param name="accessTokenGenerator">An instance of the AccessTokenGenerator service.</param>
    public PartiesClient(
        HttpClient httpClient, 
        ILogger<PartiesClient> logger, 
        IHttpContextAccessor httpContextAccessor, 
        IOptions<PlatformSettings> platformSettings,
        IAccessTokenGenerator accessTokenGenerator)
    {
        _logger = logger;
        httpClient.BaseAddress = new Uri(platformSettings.Value.ApiRegisterEndpoint);
        httpClient.DefaultRequestHeaders.Add(platformSettings.Value.SubscriptionKeyHeaderName, platformSettings.Value.SubscriptionKey);
        _client = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _platformSettings = platformSettings.Value;
        _accessTokenGenerator = accessTokenGenerator;
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <inheritdoc/>
    public async Task<Party> GetPartyAsync(int partyId, CancellationToken cancellationToken = default)
    {
        try
        {
            string endpointUrl = $"parties/{partyId}";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _platformSettings.JwtCookieName);
            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");

            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl, accessToken, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonSerializer.Deserialize<Party>(responseContent, _serializerOptions);
            }
            
            _logger.LogError("Authentication // PartiesClient // GetPartyAsync // Unexpected HttpStatusCode: {StatusCode}\n {responseContent}", response.StatusCode, responseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // PartiesClient // GetPartyAsync // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Party> GetPartyByOrgNo(string orgNo, CancellationToken cancellationToken = default)
    {
        try
        {
            string endpointUrl = $"parties/lookup";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext!, _platformSettings.JwtCookieName!);
            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");

            StringContent requestBody = new(JsonSerializer.Serialize((new PartyLookup() { OrgNo = orgNo })), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(token, endpointUrl, requestBody);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonSerializer.Deserialize<Party>(responseContent, _serializerOptions);
            }

            _logger.LogError("Authentication // PartiesClient // GetPartyByOrgNo // Unexpected HttpStatusCode: {StatusCode}\n {responseContent}", response.StatusCode, responseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // PartiesClient // GetPartyByOrgNo // Exception");
            throw;
        }
    }

    // register/api/v1/organizations/{orgNr}
    /// <inheritdoc/>
    public async Task<Organization?> GetOrganizationAsync (string orgNo, CancellationToken cancellationToken = default)
    {
        try
        {
            string endpointUrl = $"organizations/{orgNo}";

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");

            HttpResponseMessage response = await _client.GetAsync(null, endpointUrl, accessToken, cancellationToken);

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonSerializer.Deserialize<Organization>(responseContent, _serializerOptions);
            }

            _logger.LogError("Authentication // PartiesClient // GetOrganizationAsync // Unexpected HttpStatusCode: {StatusCode}\n {responseContent}", response.StatusCode, responseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // PartiesClient // GetOrganizationAsync // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, string accessPackage, CancellationToken cancellationToken)
    {
        try
        {
            CustomerRoleType customerType = GetRoleFromAccessPackage(accessPackage) switch
            {
                "regnskapsforer" => CustomerRoleType.Regnskapsforer,
                "revisor" => CustomerRoleType.Revisor,
                "forretningsforer" => CustomerRoleType.Forretningsforer,
                _ => throw new ArgumentException("Invalid customer type")
            };

            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _platformSettings.JwtCookieName);
            string endpointUrl = customerType switch
            {
                CustomerRoleType.Revisor => $"internal/parties/{partyUuid}/customers/ccr/revisor",
                CustomerRoleType.Regnskapsforer => $"internal/parties/{partyUuid}/customers/ccr/regnskapsforer",
                CustomerRoleType.Forretningsforer => $"internal/parties/{partyUuid}/customers/ccr/forretningsforer",
                _ => throw new ArgumentException("Invalid customer type")
            };

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");

            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl, accessToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return new CustomerList();
                }
                else
                {
                    return JsonSerializer.Deserialize<CustomerList>(responseContent, _serializerOptions);
                }
            }

            _logger.LogError("Authentication // RegisterClient // GetPartyCustomers // Unexpected HttpStatusCode: {StatusCode}\n {responseBody}", response.StatusCode, responseContent);
            return new Result<CustomerList>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RegisterClient // GetPartyCustomers // Exception");
            throw;
        }
    }

    private static string? GetRoleFromAccessPackage(string accessPackage)
    {
        accessPackage = $"urn:altinn:accesspackage:{accessPackage}".ToLowerInvariant();
        Dictionary<string, string> hardcodingOfAccessPackageToRole = [];
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet", "regnskapsforer");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet", "regnskapsforer");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:regnskapsforer-lonn", "regnskapsforer");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:ansvarlig-revisor", "revisor");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:revisormedarbeider", "revisor");
        hardcodingOfAccessPackageToRole.Add("urn:altinn:accesspackage:forretningsforer-eiendom", "forretningsforer");

        hardcodingOfAccessPackageToRole.TryGetValue(accessPackage, out string? found);
        return found;
    }

    /// <inheritdoc />
    public async Task<Result<CustomerList>> GetPartyCustomers(Guid partyUuid, CustomerRoleType customerType, CancellationToken cancellationToken)
    {
        try
        {
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _platformSettings.JwtCookieName);
            string endpointUrl = customerType switch
            {
                CustomerRoleType.Revisor => $"internal/parties/{partyUuid}/customers/ccr/revisor",
                CustomerRoleType.Regnskapsforer => $"internal/parties/{partyUuid}/customers/ccr/regnskapsforer",
                CustomerRoleType.Forretningsforer => $"internal/parties/{partyUuid}/customers/ccr/forretningsforer",
                _ => throw new ArgumentException("Invalid customer type")
            };

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");

            HttpResponseMessage response = await _client.GetAsync(token, endpointUrl, accessToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return new CustomerList();
                }
                else
                {
                    return JsonSerializer.Deserialize<CustomerList>(responseContent, _serializerOptions);
                }
            }

            _logger.LogError("Authentication // RegisterClient // GetPartyCustomers // Unexpected HttpStatusCode: {StatusCode}\n {responseBody}", response.StatusCode, responseContent);
            return new Result<CustomerList>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RegisterClient // GetPartyCustomers // Exception");
            throw;
        }
    }
}
