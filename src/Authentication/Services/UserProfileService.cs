using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.Models.Profile.Enums;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;
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
        private readonly ISelfIdentifiedUserCredentialRepository _selfIdentifiedUserCredentialRepository;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initialize a new instance of <see cref="UserProfileService"/> with settings for SBL Bridge endpoints.
        /// </summary>
        /// <param name="httpClient">Httpclient from httpclientfactory</param>
        /// <param name="settings">General settings for the authentication application</param>
        /// <param name="logger">A generic logger</param>
        /// <param name="selfIdentifiedUserCredentialRepository">Repository for locally stored SI credentials</param>
        /// <param name="timeProvider">Time provider used for lockout comparison (injectable for testing)</param>
        public UserProfileService(
            HttpClient httpClient,
            IOptions<GeneralSettings> settings,
            ILogger<IUserProfileService> logger,
            ISelfIdentifiedUserCredentialRepository selfIdentifiedUserCredentialRepository,
            TimeProvider timeProvider)
        {
            _client = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _selfIdentifiedUserCredentialRepository = selfIdentifiedUserCredentialRepository;
            _timeProvider = timeProvider;
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
        /// Validates a self identified user's credentials locally against
        /// <c>oidcserver.selfidentified_user_credential</c> (SHA1 + salt). This is the permanent
        /// path following the SBL Bridge decommission (the legacy <c>authentication/api/siuser</c>
        /// delegation has been removed). On success the user profile is returned.
        /// </summary>
        public async Task<UserCredentialVerificationResult> ValidateCredentialsAsync(string username, string password)
        {
            return await ValidateCredentialsLocallyAsync(username, password);
        }

        // Maximum number of consecutive failed attempts before the account is locked.
        private const int SiMaxFailedAttempts = 5;

        // Duration of the lockout window after hitting the attempt threshold.
        private static readonly TimeSpan SiLockoutDuration = TimeSpan.FromHours(1);

        /// <summary>
        /// Validates SI credentials against the locally migrated credentials in
        /// <c>oidcserver.selfidentified_user_credential</c>. Mirrors the contract of the SBL Bridge
        /// path: returns an empty result for unknown/invalid credentials (so the controller answers
        /// 401), <see cref="UserCredentialVerificationResult.IsLocked"/> when the account is
        /// temporarily locked, and a populated <see cref="UserProfile"/> on success.
        /// The username must never be logged.
        /// </summary>
        private async Task<UserCredentialVerificationResult> ValidateCredentialsLocallyAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return new UserCredentialVerificationResult();
            }

            SelfIdentifiedUserCredential? credential = await _selfIdentifiedUserCredentialRepository.GetByUsernameAsync(username);

            if (credential is null || !credential.IsActive)
            {
                return new UserCredentialVerificationResult();
            }

            // Check if the account is still within its lockout window.
            if (credential.LockoutUntil.HasValue && credential.LockoutUntil.Value > _timeProvider.GetUtcNow())
            {
                return new UserCredentialVerificationResult { IsLocked = true };
            }

            if (!VerifyAltinn2Password(password, credential.PasswordHash, credential.Salt))
            {
                await _selfIdentifiedUserCredentialRepository.RecordFailedAttemptAsync(
                    username, SiMaxFailedAttempts, SiLockoutDuration);
                return new UserCredentialVerificationResult();
            }

            // Successful login — clear any accumulated penalty.
            await _selfIdentifiedUserCredentialRepository.ResetFailedAttemptsAsync(username);

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

#nullable enable
        /// <inheritdoc/>
        public async Task<SelfIdentifiedLinkTarget?> GetSelfIdentifiedLinkTargetAsync(string? username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            SelfIdentifiedUserCredential? credential =
                await _selfIdentifiedUserCredentialRepository.GetByUsernameAsync(username, cancellationToken);

            // A single null result covers every "cannot proceed" case (unknown, inactive, no email),
            // so the caller responds identically and does not reveal which one. The username must not
            // be logged.
            if (credential is null || !credential.IsActive || string.IsNullOrEmpty(credential.Email))
            {
                return null;
            }

            return new SelfIdentifiedLinkTarget
            {
                PartyUuid = credential.PartyUuid,
                Email = credential.Email
            };
        }
#nullable restore

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
