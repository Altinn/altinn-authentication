using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Controllers;
using Altinn.Platform.Authentication.Model;
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
        public async Task SystemUser_Get_Single_ReturnsOK()
        {
            SystemUserResponse test = await _systemUserService.GetSingleSystemUserById(Guid.Parse("37ce1792-3b35-4d50-a07d-636017aa7dbd"));
                        
            Assert.True(test is not null);
        }

        [Fact]
        public async Task SystemUser_Get_ListForPartyId_ReturnsListOK()
        {
            List<SystemUserResponse> test = await _systemUserService.GetListOfSystemUsersPartyHas(1);

            Assert.True(test is not null);
            Assert.True(test.Count > 0);
        }
    }
}
