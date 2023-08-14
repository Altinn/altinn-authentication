using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Clients
{
    /// <summary>
    /// Implementation of the <see ref="IEventsQueueClient"/> using Azure Storage Queues.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventsQueueClient : IEventsQueueClient
    {
        private readonly QueueStorageSettings _settings;

        private QueueClient _authenticationEventQueueClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsQueueClient"/> class.
        /// </summary>
        /// <param name="settings">The queue storage settings</param>
        public EventsQueueClient(IOptions<QueueStorageSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <inheritdoc/>
        public async Task<QueuePostReceipt> EnqueueAuthenticationEvent(string content)
        {
            try
            {
                QueueClient client = await GetAuthenticationEventQueueClient();
                Response<SendReceipt> response = await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));      
            }
            catch (Exception e)
            {
                return new QueuePostReceipt { Success = false, Exception = e };
            }

            return new QueuePostReceipt { Success = true };
        }

        private async Task<QueueClient> GetAuthenticationEventQueueClient()
        {
            try
            {
                if (_authenticationEventQueueClient == null)
                {
                    _authenticationEventQueueClient = new QueueClient(_settings.ConnectionString, _settings.AuthenticationEventQueueName);
                    await _authenticationEventQueueClient.CreateIfNotExistsAsync();
                }

                return _authenticationEventQueueClient;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
