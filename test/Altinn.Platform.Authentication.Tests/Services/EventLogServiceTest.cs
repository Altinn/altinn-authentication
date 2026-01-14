using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Mocks;
using Azure.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Microsoft.FeatureManagement;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    public class EventLogServiceTest
    {
        private readonly FakeTimeProvider timeProviderMock = new();

        [Fact]
        public async Task QueueAuthenticationEvent_OK()
        {
            // Arrange            
            UserAuthenticationModel authenticatedUser = GetAuthenticationModel(SecurityLevel.QuiteSensitive, AuthenticationMethod.AltinnPIN, 45321);

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IFeatureManager> featureManageMock = new Mock<IFeatureManager>();
            featureManageMock
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(true));
            var service = GetEventLogService(queueMock: queueMock.Object, timeProviderMock);
            Mock<HttpContext> context = new Mock<HttpContext>();

            // Act
            service.CreateAuthenticationEventAsync(featureManageMock.Object, authenticatedUser, AuthenticationEventType.Authenticate, context.Object);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task QueueAuthenticationEvent_token_OK()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();

            string orgNr = "974760223";
            string digdirOrgNo = "991825827";

            object digdirConsumer = new
            {
                authority = "iso6523-actorid-upis",
                ID = $"9908:{digdirOrgNo}"
            };

            claims.Add(new Claim("consumer", JsonConvert.SerializeObject(digdirConsumer)));
            claims.Add(new Claim("client_orgno", orgNr));
            claims.Add(new Claim("scope", "altinn:serviceowner/instances.read altinn:serviceowner/instances.write"));
            claims.Add(new Claim("iss", "https://ver2.maskinporten.no/"));

            ClaimsIdentity identity = new ClaimsIdentity("OrganisationLogin");
            identity.AddClaims(claims);
            ClaimsPrincipal externalPrincipal = new ClaimsPrincipal(identity);

            string externalToken = JwtTokenMock.GenerateToken(externalPrincipal, TimeSpan.FromMinutes(2));

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IFeatureManager> featureManageMock = new Mock<IFeatureManager>();
            featureManageMock
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(true));
            var service = GetEventLogService(queueMock: queueMock.Object, timeProviderMock);
            Mock<HttpContext> context = new Mock<HttpContext>();

            IPAddress address = IPAddress.Parse("255.1.3.12");

            // Act
            service.CreateAuthenticationEventAsync(featureManageMock.Object, externalToken, AuthenticationEventType.Authenticate, address);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task QueueAuthenticationEvent_Error()
        {
            // Arrange
            UserAuthenticationModel authenticatedUser = null;

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IFeatureManager> featureManageMock = new Mock<IFeatureManager>();
            featureManageMock
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(true));
            var service = GetEventLogService(queueMock: queueMock.Object, timeProviderMock);
            Mock<HttpContext> context = new Mock<HttpContext>();

            // Act
            service.CreateAuthenticationEventAsync(featureManageMock.Object, authenticatedUser, AuthenticationEventType.Authenticate, context.Object);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task QueueAuthenticationEvent_Token_Error()
        {
            // Arrange
            string token = null;

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueAuthenticationEvent(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IFeatureManager> featureManageMock = new Mock<IFeatureManager>();
            featureManageMock
                .Setup(m => m.IsEnabledAsync("AuditLog"))
                .Returns(Task.FromResult(true));
            var service = GetEventLogService(queueMock: queueMock.Object, timeProviderMock);
            Mock<HttpContext> context = new Mock<HttpContext>();

            IPAddress ipadress = IPAddress.Parse("244.233.12.2");

            // Act
            service.CreateAuthenticationEventAsync(featureManageMock.Object, token, AuthenticationEventType.Authenticate, ipadress);

            queueMock.Verify(r => r.EnqueueAuthenticationEvent(It.IsAny<string>()), Times.Never);
        }

        private static IEventLog GetEventLogService(IEventsQueueClient queueMock = null, TimeProvider timeProviderMock = null)
        {
            if (queueMock == null)
            {
                queueMock = new EventsQueueClientMock();
            }

            var service = new EventLogService(queueMock, timeProviderMock);
            return service;
        }

        private static UserAuthenticationModel GetAuthenticationModel(SecurityLevel authenticationLevel, AuthenticationMethod authenticationMethod, int userId)
        {
            UserAuthenticationModel authenticatedUser = new()
            {
                AuthenticationLevel = authenticationLevel,
                AuthenticationMethod = authenticationMethod,
                IsAuthenticated = true,
                UserID = userId                
            };

            return authenticatedUser;
        }
    }
}
