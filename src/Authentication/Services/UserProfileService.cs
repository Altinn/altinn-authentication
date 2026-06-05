using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Services
{
    /// <inheritdoc/>
    public class UserProfileService : IUserProfileService
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private readonly GeneralSettings _settings;
        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private readonly IFeatureManager _featureManager;
        private readonly ISelfIdentifiedUserCredentialRepository _selfIdentifiedUserCredentialRepository;

        /// <summary>
        /// Initialize a new instance of <see cref="UserProfileService"/> with settings for SBL Bridge endpoints.
        /// </summary>
        /// <param name="httpClient">Httpclient from httpclientfactory</param>
        /// <param name="settings">General settings for the authentication application</param>
        /// <param name="logger">A generic logger</param>
        /// <param name="featureManager">Feature manager used to switch SI credential validation to the local database</param>
        /// <param name="selfIdentifiedUserCredentialRepository">Repository for locally stored SI credentials</param>
        public UserProfileService(
            HttpClient httpClient,
            IOptions<GeneralSettings> settings,
            ILogger<IUserProfileService> logger,
            IFeatureManager featureManager,
            ISelfIdentifiedUserCredentialRepository selfIdentifiedUserCredentialRepository)
        {
            _client = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _featureManager = featureManager;
            _selfIdentifiedUserCredentialRepository = selfIdentifiedUserCredentialRepository;
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
        /// Validates a self identified user's credentials. When the
        /// <see cref="FeatureFlags.LocalSelfIdentifiedCredentialValidation"/> flag is enabled the
        /// check is performed locally against <c>oidcserver.selfidentified_user_credential</c>
        /// (SHA1 + salt); otherwise it is delegated to the SBL Bridge authentication API
        /// (<c>authentication/api/siuser</c>). On success the user profile is returned.
        /// </summary>
        public async Task<UserCredentialVerificationResult> ValidateCredentialsAsync(string username, string password)
        {
            if (await _featureManager.IsEnabledAsync(FeatureFlags.LocalSelfIdentifiedCredentialValidation))
            {
                return await ValidateCredentialsLocallyAsync(username, password);
            }

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

        /// <summary>
        /// Validates SI credentials against the locally migrated credentials in
        /// <c>oidcserver.selfidentified_user_credential</c>. Mirrors the contract of the SBL Bridge
        /// path: returns an empty result for unknown/invalid credentials (so the controller answers
        /// 401) and a populated <see cref="UserProfile"/> on success.
        /// </summary>
        /// <remarks>
        /// Lockout/throttling and the expired-password policy are not yet enforced here - both are
        /// tracked as open items in issue #2025. The username must never be logged.
        /// </remarks>
        private async Task<UserCredentialVerificationResult> ValidateCredentialsLocallyAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return new UserCredentialVerificationResult();
            }

            SelfIdentifiedUserCredential credential = await _selfIdentifiedUserCredentialRepository.GetByUsernameAsync(username);

            if (credential is null || !credential.IsActive || !VerifyAltinn2Password(password, credential.PasswordHash, credential.Salt))
            {
                return new UserCredentialVerificationResult();
            }

            return new UserCredentialVerificationResult
            {
                UserProfile = new UserProfile
                {
                    UserId = credential.UserId,
                    UserUuid = credential.PartyUuid,
                    UserName = credential.UserName,
                    UserType = UserType.SelfIdentified,
                    Party = new Party { PartyUuid = credential.PartyUuid }
                }
            };
        }

        /// <summary>
        /// Verifies a plaintext password against an Altinn 2 SHA1 hash and salt, using the same
        /// algorithm as Altinn 2: <c>Base64( SHA1( UTF8(password) || Base64Decode(salt) ) )</c>.
        /// Uses a fixed-time comparison to avoid timing attacks.
        /// </summary>
        private static bool VerifyAltinn2Password(string plaintext, string storedHash, string storedSalt)
        {
            if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            {
                return false;
            }

            byte[] saltBytes;
            try
            {
                saltBytes = Convert.FromBase64String(storedSalt);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] combined = new byte[plaintextBytes.Length + saltBytes.Length];
            Buffer.BlockCopy(plaintextBytes, 0, combined, 0, plaintextBytes.Length);
            Buffer.BlockCopy(saltBytes, 0, combined, plaintextBytes.Length, saltBytes.Length);

            byte[] hashBytes = SHA1.HashData(combined);
            string computed = Convert.ToBase64String(hashBytes);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(storedHash));
        }
    }
}
