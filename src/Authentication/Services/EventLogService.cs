#nullable enable

using System;
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
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Instantiation for event log servcie
        /// </summary>
        /// <param name="queueClient">queue client to store event in event log</param>
        /// <param name="timeProvider">the timeprovider to get the current datetime</param>
        public EventLogService(IEventsQueueClient queueClient, TimeProvider timeProvider)
        {
            _queueClient = queueClient;
            _timeProvider = timeProvider;
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
                AuthenticationEvent authenticationEvent = EventlogHelper.MapAuthenticationEvent(authenticatedUser, eventType, context, _timeProvider.GetUtcNow());
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
        /// <param name="ipadress">The ip adress of the caller</param>
        /// <param name="externalSessionId">the external session id</param>
        public async Task CreateAuthenticationEventAsync(
            IFeatureManager featureManager, 
            string jwtToken, 
            AuthenticationEventType eventType, 
            System.Net.IPAddress ipadress,
            string? externalSessionId = null) 
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                AuthenticationEvent authenticationEvent = EventlogHelper.MapAuthenticationEvent(jwtToken, eventType, ipadress, _timeProvider.GetUtcNow(), externalSessionId);
                if (authenticationEvent != null)
                {
                    _queueClient.EnqueueAuthenticationEvent(JsonSerializer.Serialize(authenticationEvent));
                }
            }
        }
    }
}
