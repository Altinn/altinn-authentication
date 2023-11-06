using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;

using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// Unit Tests for the SystemUnitController
    /// </summary>
    public class SystemUserControllerTest :IClassFixture<WebApplicationFactory<SystemUserController>>
    {
        private readonly WebApplicationFactory<SystemUserController> _factory;
        private readonly ISystemUserService _systemUserService;

        public SystemUserControllerTest(WebApplicationFactory<SystemUserController> factory)
        {
            _factory = factory;
            _systemUserService = new SystemUserService();
        }

        [Fact]
        public async Task GetSystemUserListForPartyId_ReturnsListOK()
        {
            var test = _systemUserService.GetSingleSystemUserById(Guid.Empty);

            Assert.True(test is not null);
        }
    }
}
