using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Util to handle configuration managers. Singleton pattern
    /// </summary>
    public static class ConfigurationMangerHelper
    {
        private static ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configManagers = new();

        /// <summary>
        /// Get the configuration published by the given endpoint.
        /// </summary>
        /// <param name="wellKnownEndpoint">The url of the endpoint</param>
        /// <returns>The configuration published by the given endpoint</returns>
        public static async Task<OpenIdConnectConfiguration> GetOidcConfiguration(string wellKnownEndpoint)
        {
            string configKey = wellKnownEndpoint.ToLower();
            if (!_configManagers.TryGetValue(configKey, out ConfigurationManager<OpenIdConnectConfiguration> configurationManager))
            {
                configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                   wellKnownEndpoint,
                   new OpenIdConnectConfigurationRetriever(),
                   new HttpDocumentRetriever());
                _configManagers[configKey] = configurationManager;
            }

            return await configurationManager.GetConfigurationAsync();
        }
    }
}
