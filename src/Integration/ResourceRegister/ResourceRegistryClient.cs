﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;

namespace Altinn.Platform.Authentication.Integration.ResourceRegister
{
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
                string endpointUrl = $"resource/{resourceId}";

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
    }
}
