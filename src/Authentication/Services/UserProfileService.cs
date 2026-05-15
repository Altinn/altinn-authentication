using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services
{
    /// <inheritdoc/>
    public class UserProfileService : IUserProfileService
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private readonly GeneralSettings _settings;
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        /// <summary>
        /// Initialize a new instance of <see cref="UserProfileService"/> with settings for SBL Bridge endpoints.
        /// </summary>
        /// <param name="httpClient">Httpclient from httpclientfactory</param>
        /// <param name="settings">General settings for the authentication application</param>
        /// <param name="logger">A generic logger</param>
        public UserProfileService(HttpClient httpClient, IOptions<GeneralSettings> settings, ILogger<IUserProfileService> logger)
        {
            _client = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<UserProfile> GetUser(string ssnOrExternalIdentity)
        {
            UserProfile user = null;
       
            Uri endpointUrl = new Uri($"{_settings.BridgeProfileApiEndpoint}users/");
            StringContent requestBody = new StringContent(JsonSerializer.Serialize(ssnOrExternalIdentity), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                user = await response.Content.ReadFromJsonAsync<UserProfile>(_options);
            }
            else
            {
                _logger.LogError("Getting user by SSN or external identity failed with statuscode {StatusCode}", response.StatusCode);
            }

            return user;
        }

        /// <summary>
        /// Method to create a new user based on identity
        /// </summary>
        /// <param name="user">The userprofile</param>
        /// <returns>The created users with userId and partyID</returns>
        public async Task<UserProfile> CreateUser(UserProfile user)
        {
            UserProfile createdProfile = null;

            Uri endpointUrl = new Uri($"{_settings.BridgeProfileApiEndpoint}users/create/");
            StringContent requestBody = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                createdProfile = await response.Content.ReadFromJsonAsync<UserProfile>(_options);
            }
            else
            {
                _logger.LogError("Creating user failed for externalIdentity {ExternalIdentity}", user.ExternalIdentity);
            }

            return createdProfile;
        }

        /// <summary>
        /// Validates a self identified user's credentials against the SBL Bridge authentication API
        /// (<c>authentication/api/siuser</c>), and returns the user profile when the credentials are valid.
        /// </summary>
        public async Task<UserCredentialVerificationResult> ValidateCredentialsAsync(string username, string password)
        {
            UserProfile identifedProfile = null;

            SiUserCredentials credentials = new SiUserCredentials()
            {
                UserName = username,
                Password = password
            };

            Uri endpointUrl = new Uri($"{_settings.BridgeAuthnApiEndpoint}siuser");
            using StringContent requestBody = new StringContent(JsonSerializer.Serialize(credentials), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                identifedProfile = await response.Content.ReadFromJsonAsync<UserProfile>(_options);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Bridge returns 429 when the account is locked out due to too many failed attempts.
                return new UserCredentialVerificationResult()
                {
                    IsLocked = true
                };
            }
            else if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
            {
                // Bridge returns 404 when the credentials are not authenticated and 400 for empty
                // username/password. Both are expected outcomes - not errors, and the username must not be logged.
                return new UserCredentialVerificationResult();
            }
            else
            {
                _logger.LogError("Validating user credentials failed with statuscode {StatusCode}", response.StatusCode);
                return new UserCredentialVerificationResult();
            }

            if (identifedProfile != null && identifedProfile.UserType != Core.Models.Profile.Enums.UserType.SelfIdentified)
            {
                return new UserCredentialVerificationResult()
                {
                    WrongUserType = true
                };
            }

            UserCredentialVerificationResult result = new UserCredentialVerificationResult();
            result.UserProfile = identifedProfile; 

            return result;
        }
    }
}
