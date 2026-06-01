using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Represents a service that can decrypt an SBL .ASPXAUTH cookie.
    /// </summary>
    public class SblCookieDecryptionService : ISblCookieDecryptionService
    {
        private readonly ILogger<SblCookieDecryptionService> _logger;
        private readonly GeneralSettings _generalSettings;
        private readonly IFeatureManager _featureManager;

        private readonly HttpClient _client;

        /// <summary>
        /// Initialize a new instance of <see cref="SblCookieDecryptionService"/>  with settings for SBL Bridge endpoints.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to use when performing requests against SblBridge.</param>
        /// <param name="generalSettings">General settings for the authentication application.</param>
        /// <param name="logger">A generic logger.</param>
        /// <param name="featureManager">Feature manager used to gate the SBL Bridge call.</param>
        public SblCookieDecryptionService(
            HttpClient httpClient, IOptions<GeneralSettings> generalSettings, ILogger<SblCookieDecryptionService> logger, IFeatureManager featureManager)
        {
            _client = httpClient;
            _logger = logger;
            _generalSettings = generalSettings.Value;
            _featureManager = featureManager;
        }

        /// <inheritdoc />
        public async Task<UserAuthenticationModel> DecryptTicket(string encryptedTicket)
        {
            if (await _featureManager.IsEnabledAsync(FeatureFlags.CookieTicketDecryptionDisabled))
            {
                _logger.LogInformation("Cookie ticket decryption is disabled by feature flag {Flag}", FeatureFlags.CookieTicketDecryptionDisabled);
                return null;
            }

            DataContractJsonSerializer serializer = new(typeof(UserAuthenticationModel));
            Uri endpointUrl = new Uri($"{_generalSettings.BridgeAuthnApiEndpoint}tickets");

            string userData = JsonSerializer.Serialize(new UserAuthenticationModel { EncryptedTicket = encryptedTicket });

            HttpResponseMessage response =
                await _client.PostAsync(endpointUrl, new StringContent(userData, Encoding.UTF8, "application/json"));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream stream = await response.Content.ReadAsStreamAsync();
                UserAuthenticationModel userAuthentication = serializer.ReadObject(stream) as UserAuthenticationModel;

                return userAuthentication;
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new SblBridgeResponseException(response, "SBL Bridge replied with status: ServiceUnavailable.");
            }

            _logger.LogError("Getting the authenticated user failed with status code {StatusCode}", response.StatusCode);

            return null;
        }
    }
}
