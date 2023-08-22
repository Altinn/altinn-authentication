using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper class for event logging
    /// </summary>
    public static class EventlogHelper
    {
        /// <summary>
        /// Creates an authentication event
        /// </summary>
        /// <param name="featureManager">handler for feature manager service</param>
        /// <param name="eventLog">handler for eventlog service</param>
        /// <param name="userAuthentication">authentication object</param>
        public async static void CreateAuthenticationEvent(IFeatureManager featureManager, IEventLog eventLog, UserAuthenticationModel userAuthentication)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                eventLog.CreateAuthenticationEvent(userAuthentication);
            }
        }
    }
}
