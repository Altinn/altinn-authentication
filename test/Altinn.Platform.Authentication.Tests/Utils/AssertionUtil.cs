﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Clients.Interfaces;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Model;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class AssertionUtil
    {
        public static void AssertAuthenticationEvent(Mock<IEventsQueueClient> eventQueue, AuthenticationEvent expectedAuthenticationEvent, Times numberOfTimes)
        {
            string serializedAuthenticationEvent = JsonConvert.SerializeObject(expectedAuthenticationEvent);
            eventQueue.Verify(e => e.EnqueueAuthenticationEvent(It.Is<string>(q => q == serializedAuthenticationEvent)), numberOfTimes);
        }

        public static void AssertRegisteredSystem(RegisteredSystemResponse expected, RegisteredSystemResponse actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.InternalId, actual.InternalId);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.SystemVendorOrgName, actual.SystemVendorOrgName);
            Assert.Equal(expected.SystemVendorOrgNumber, actual.SystemVendorOrgNumber);            
            Assert.Equal(expected.ClientId, actual.ClientId);
            Assert.Equal(expected.IsVisible, actual.IsVisible);
            Assert.Equal(expected.IsDeleted, actual.IsDeleted);
            Assert.Equal(expected.Rights.Count, actual.Rights.Count);
            Assert.Equal(expected.AllowedRedirectUrls, actual.AllowedRedirectUrls);
        }

        public static void AssertRegisteredSystemDTO(RegisteredSystemDTO expected, RegisteredSystemDTO actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.SystemId, actual.SystemId);
            Assert.Equal(expected.SystemVendorOrgName, actual.SystemVendorOrgName);
            Assert.Equal(expected.SystemVendorOrgNumber, actual.SystemVendorOrgNumber);
            Assert.Equal(expected.Rights.Count, actual.Rights.Count);
        }
    }
}
