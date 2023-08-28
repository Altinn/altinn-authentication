using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class EventsQueueClientMock : IEventsQueueClient
    {
        public Task<QueuePostReceipt> EnqueueAuthenticationEvent(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }
    }
}
