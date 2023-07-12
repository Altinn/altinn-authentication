using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Azure.Messaging;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Implementation for authentication event log
    /// </summary>
    public class EventLogService : IEventLog
    {
        private readonly IEventsQueueClient _queueClient;

        /// <summary>
        /// Instantiation for event log servcie
        /// </summary>
        /// <param name="queueClient">queue client to store event in event log</param>
        public EventLogService(IEventsQueueClient queueClient)
        {
            _queueClient = queueClient;
        }

        /// <summary>
        /// Queues an authentication event to the logqueue
        /// </summary>
        /// <param name="authenticationEvent">authentication event to be stored in the queue</param>
        public void CreateAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            _queueClient.EnqueueAuthenticationEvent(JsonSerializer.Serialize(authenticationEvent));
        }
    }
}
