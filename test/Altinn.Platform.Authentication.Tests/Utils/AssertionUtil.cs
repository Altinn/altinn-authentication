using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Model;
using Moq;
using Newtonsoft.Json;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class AssertionUtil
    {
        public static void AssertAuthenticationEvent(Mock<IEventsQueueClient> eventQueue, AuthenticationEvent expectedAuthenticationEvent, Times numberOfTimes)
        {
            string serializedAuthenticationEvent = JsonConvert.SerializeObject(expectedAuthenticationEvent);
            eventQueue.Verify(e => e.EnqueueAuthenticationEvent(It.Is<string>(q => q == serializedAuthenticationEvent)), numberOfTimes);
        }
    }
}
