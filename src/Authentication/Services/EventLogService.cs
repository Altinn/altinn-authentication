using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Azure.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Implementation for authentication event log
    /// </summary>
    public class EventLogService : IEventLog
    {
        private readonly IEventsQueueClient _queueClient;
        private readonly ISystemClock _systemClock;

        /// <summary>
        /// Instantiation for event log servcie
        /// </summary>
        /// <param name="queueClient">queue client to store event in event log</param>
        public EventLogService(IEventsQueueClient queueClient, ISystemClock systemClock)
        {
            _queueClient = queueClient;
            _systemClock = systemClock;
        }

        /// <summary>
        /// Creates an authentication event
        /// </summary>
        /// <param name="featureManager">handler for feature manager service</param>
        /// <param name="authenticatedUser">authenticat</param>
        /// <param name="eventType">authentication event type</param>
        /// <param name="context">the http context</param>
        public async Task CreateAuthenticationEventAsync(IFeatureManager featureManager, UserAuthenticationModel authenticatedUser, AuthenticationEventType eventType, HttpContext context)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                AuthenticationEvent authenticationEvent = EventlogHelper.MapAuthenticationEvent(authenticatedUser, eventType, context, _systemClock.UtcNow.DateTime);
                if (authenticationEvent != null)
                {
                    _queueClient.EnqueueAuthenticationEvent(JsonSerializer.Serialize(authenticationEvent));
                }
            }
        }

        /// <summary>
        /// Creates an authentication event
        /// </summary>
        /// <param name="featureManager">handler for feature manager service</param>
        /// <param name="jwtToken">the token cookie with user information</param>
        /// <param name="eventType">authentication event type</param>
        /// <param name="context">the http context</param>
        public async Task CreateAuthenticationEventAsync(IFeatureManager featureManager, string jwtToken, AuthenticationEventType eventType, HttpContext context)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                AuthenticationEvent authenticationEvent = EventlogHelper.MapAuthenticationEvent(jwtToken, eventType, context, _systemClock.UtcNow.DateTime);
                if (authenticationEvent != null)
                {
                    _queueClient.EnqueueAuthenticationEvent(JsonSerializer.Serialize(authenticationEvent));
                }
            }
        }
    }
}
