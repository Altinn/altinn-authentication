using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using System.Security.Policy;
using Altinn.Platform.Authentication.Core.Models.Rights;
using System.Net.Http.Json;

namespace Altinn.Platform.Authentication.Integration.ResourceRegister
{
    /// <summary>
    /// Client implementation for integration with the Resource Registry
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResourceRegistryClient : IResourceRegistryClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IResourceRegistryClient> _logger;
        private readonly PlatformSettings _platformSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _serializerOptions =
            new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Initializes a new instance of the <see cref="LookupClient"/> class
        /// </summary>
        /// <param name="httpClient">HttpClient from default httpclientfactory</param>
        /// <param name="logger">the logger</param>
        /// <param name="httpContextAccessor">handler for http context</param>
        /// <param name="platformSettings">the platform setttings</param>
        public ResourceRegistryClient(
            HttpClient httpClient,
            ILogger<ResourceRegistryClient> logger,
            IHttpContextAccessor httpContextAccessor,
            IOptions<PlatformSettings> platformSettings)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _platformSettings = platformSettings.Value;
            httpClient.BaseAddress = new Uri(_platformSettings.ApiResourceRegistryEndpoint!);
            _httpClient = httpClient;
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public async Task<ServiceResource?> GetResource(string resourceId)
        {
            ServiceResource resource = null;

            try
            {
                // It's not possible to filter on AltinnApp or Altinn2Service for this endpoint
                string endpointUrl = $"resource/{HttpUtility.UrlEncode(resourceId)}";

                HttpResponseMessage response = await _httpClient.GetAsync(endpointUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    JsonSerializerOptions options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };
                    string content = await response.Content.ReadAsStringAsync();
                    resource = JsonSerializer.Deserialize<ServiceResource>(content, options);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // ResourceRegistryClient // SearchResources // Exception");
                throw;
            }

            return resource;
        }

        public async Task<List<PolicyRightsDTO>> GetRights(string resourceId)
        {
            try
            {
                string endpointUrl = $"resource/{HttpUtility.UrlEncode(resourceId)}/policy/rights";

                HttpResponseMessage response = await _httpClient.GetAsync(endpointUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var policyRightsDTOs = await response.Content.ReadFromJsonAsync<List<PolicyRightsDTO>>(_serializerOptions);
                    if (policyRightsDTOs is not null)
                    {
                        return policyRightsDTOs;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // ResourceRegistryClient // GetRights // Exception");
                throw;
            }
            return [];
        }
    }
}
