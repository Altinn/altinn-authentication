using System.Threading.Tasks;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Defines event log interface to queue an authentication event to a storage queue
    /// </summary>
    public interface IEventLog
    {
        /// <summary>
        /// Creates an authentication event in storage queue
        /// </summary>
        /// <param name="featureManager">the feature manager handler</param>
        /// <param name="authenticatedUser">the authenticated user information</param>
        /// <param name="eventType">type of authentication event</param>
        /// <param name="context">the http context</param>
        /// <returns></returns>
        public Task CreateAuthenticationEventAsync(IFeatureManager featureManager, UserAuthenticationModel authenticatedUser, AuthenticationEventType eventType, HttpContext context);

        /// <summary>
        /// Creates an authentication event in storage queue
        /// </summary>
        /// <param name="featureManager">the feature manager handler</param>
        /// <param name="jwttoken">the authenticated user information in token</param>
        /// <param name="eventType">type of authentication event</param>
        /// <param name="context">the http context</param>
        /// <returns></returns>
        public Task CreateAuthenticationEventAsync(IFeatureManager featureManager, string jwttoken, AuthenticationEventType eventType, HttpContext context);

    }
}
