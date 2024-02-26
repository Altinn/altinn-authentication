using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    [ExcludeFromCodeCoverage]
    public class SystemRegisterServiceMock : ISystemRegisterService
    {
        private readonly List<RegisteredSystem> _registeredSystemsMockList;

        public SystemRegisterServiceMock()
        {
            _registeredSystemsMockList = MockDataHelper();
        }

        public async Task<List<RegisteredSystem>> GetListRegSys(CancellationToken cancellation = default)
        {
            await Task.Delay(50);
            return _registeredSystemsMockList;
        }
                
        private static List<RegisteredSystem> MockDataHelper() 
        {
            RegisteredSystem reg1 = new()
            {
                SystemVendor = "Awesome",
                SystemTypeId = "Tax",
                Description = "Awesome_Tax"
            };

            RegisteredSystem reg2 = new()
            {
                SystemVendor = "Wonderful",
                SystemTypeId = "Tax",
                Description = "Wonderful_Tax"
            };

            RegisteredSystem reg3 = new()
            {
                SystemVendor = "Brilliant",
                SystemTypeId = "HR",
                Description = "Brilliant_HR"
            };

            RegisteredSystem reg4 = new()
            {
                SystemVendor = "Fantastic",
                SystemTypeId = "HR",
                Description = "Fantastic_HR"
            };

            List<RegisteredSystem> list = new()
            {
                reg1, reg2, reg3, reg4
            };

            return list;
        }
    }
}
