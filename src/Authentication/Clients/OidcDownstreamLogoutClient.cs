using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Altinn.Platform.Authentication.Core.Clients.Interfaces;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Authentication.Clients
{
    /// <summary>
    /// CLient that tries to logout the 
    /// </summary>
    public class OidcDownstreamLogoutClient(HttpClient httpClient, ILogger<OidcDownstreamLogoutClient> logger) : IOidcDownstreamLogout
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<OidcDownstreamLogoutClient> _logger = logger;

        /// <summary>
        /// Tries to do a front channel logout against the downstream client. 
        /// This is not a standard way to log out a downstream OIDC client, but since Altinn Authenticaiton 
        /// does work as a client itself we cant just put an ifram inside the iframe to log out our downstream clients.
        /// </summary>
        public async Task<bool> TryLogout(OidcClient oidcClient, string sessionId, string iss)
        {
            if (oidcClient.FrontchannelLogoutUri == null)
            {
                _logger.LogDebug("No front channel logout URI configured for client {ClientId}", oidcClient.ClientId);
                return true; // Not an error if not configured
            }

            try
            {
                var uriBuilder = new UriBuilder(oidcClient.FrontchannelLogoutUri);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                
                if (!string.IsNullOrEmpty(sessionId))
                {
                    query["sid"] = sessionId;
                }
                
                if (!string.IsNullOrEmpty(iss))
                {
                    query["iss"] = iss;
                }
                
                uriBuilder.Query = query.ToString();
                var logoutUri = uriBuilder.Uri;

                _logger.LogDebug("Calling front channel logout for client {ClientId} at {LogoutUri}", oidcClient.ClientId, logoutUri);

                var response = await _httpClient.GetAsync(logoutUri);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Front channel logout succeeded for client {ClientId}", oidcClient.ClientId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Front channel logout failed for client {ClientId} with status {StatusCode}", oidcClient.ClientId, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during front channel logout for client {ClientId}", oidcClient.ClientId);
                return false;
            }
        }
    }
}
