using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Mocks;
using Azure.Messaging;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    public class EventLogServiceTest
    {
        //public EventLogServiceTest() { }
        
        [Fact]
        public async Task QueueAuthenticationEvent_OK()
        {
            // Arrange
            //AuthenticationEvent authenticationEvent = GetAuthenticationEvent();
            UserAuthenticationModel userAuthenticationModel = GetUserAuthenticationModel(SecurityLevel.QuiteSensitive, AuthenticationMethod.AltinnPIN, true, 45321);

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            var service = GetEventLogService(queueMock: queueMock.Object);

            // Act
            service.CreateAuthenticationEvent(userAuthenticationModel);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task QueueAuthenticationEvent_Error()
        {
            // Arrange
            //AuthenticationEvent authenticationEvent = GetAuthenticationEvent();
            UserAuthenticationModel userAuthenticationModel = null;

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            var service = GetEventLogService(queueMock: queueMock.Object);

            // Act
            service.CreateAuthenticationEvent(userAuthenticationModel);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Never);
        }

        private static IEventLog GetEventLogService(IEventsQueueClient queueMock = null)
        {
            if (queueMock == null)
            {
                queueMock = new EventsQueueClientMock();
            }

            var service = new EventLogService(queueMock);
            return service;
        }

        private static UserAuthenticationModel GetUserAuthenticationModel(SecurityLevel authenticationLevel, AuthenticationMethod authenticationMethod, bool isAuthenticated, int userId)
        {
            UserAuthenticationModel authenticationModel = new()
            {
                AuthenticationLevel = authenticationLevel,
                AuthenticationMethod = authenticationMethod,
                IsAuthenticated = isAuthenticated,
                UserID = userId
            };

            return authenticationModel;
        }
    }
}
