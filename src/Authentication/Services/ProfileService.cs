using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql.Internal;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Profile service that handles operations related to user profiles
    /// </summary>
    public class ProfileService : IProfile
    {
        private readonly HttpClient _profileClient;
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly ILogger _logger;
        private readonly GeneralSettings _generalSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class
        /// </summary>
        public ProfileService(HttpClient httpClient, ILogger<ProfileService> logger, IOptions<GeneralSettings> generalSettings)
        {
            _profileClient = httpClient;
            _generalSettings = generalSettings.Value;
            _profileClient.BaseAddress = new Uri(_generalSettings.ProfileAPIEndpoint);
            _logger = logger;
        }

        /// <summary>
        /// Returns the user profile for a given user
        /// </summary>
        public async Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup)
        {
            try
            {
                string endpointUrl = $"internal/user";

                var response = await _profileClient.PostAsJsonAsync(endpointUrl, profileLookup);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return JsonSerializer.Deserialize<UserProfile>(responseContent, _serializerOptions);
                }

                _logger.LogError("ProfileAPI // ProfileWrapper // GetUserProfile // Failed // Unexpected HttpStatusCode: {statusCode}\n {responseContent}", response.StatusCode, responseContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProfileAPI // ProfileWrapper // GetUserProfile // Failed // Unexpected Exception");
                throw;
            }
        }
    }
}
