using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
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
                SystemTypeId = "Awesome_Tax",
                Description = "Awesome_Tax",
                DefaultRights =
                [
                    new()
                    {
                        ActionRight = "Read",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair 
                            { 
                                Id = "urn:altinn:app", 
                                Value = "mva"
                            }
                        ]
                    },
                    new()
                    {
                        ActionRight = "Write",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "mva"
                            }
                        ]
                    }
                ]
            };

            RegisteredSystem reg2 = new()
            {
                SystemVendor = "Wonderful",
                SystemTypeId = "Wonderful_Tax",
                Description = "Wonderful_Tax",
                DefaultRights =
                [
                    new()
                    {
                        ActionRight = "Read",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "mva"
                            }
                        ]
                    },
                    new()
                    {
                        ActionRight = "Write",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "mva"
                            }
                        ]
                    }
                ]
            };

            RegisteredSystem reg3 = new()
            {
                SystemVendor = "Brilliant",
                SystemTypeId = "Brilliant_HR",
                Description = "Brilliant_HR",
                DefaultRights =
                [
                    new()
                    {
                        ActionRight = "Read",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "lonn"
                            }
                        ]
                    },
                    new()
                    {
                        ActionRight = "Write",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "lonn"
                            }
                        ]
                    }
                ]
            };

            RegisteredSystem reg4 = new()
            {
                SystemVendor = "Fantastic",
                SystemTypeId = "Fantastic_HR",
                Description = "Fantastic_HR",
                DefaultRights =
                [
                    new()
                    {
                        ActionRight = "Read",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "lonn"
                            }
                        ]
                    },
                    new()
                    {
                        ActionRight = "Write",
                        ServiceProvider = "Skatteetaten",
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:app",
                                Value = "lonn"
                            }
                        ]
                    }
                ]
            };

            List<RegisteredSystem> list =
                [
                    reg1, reg2, reg3, reg4
                ];

            return list;
        }

        public async Task<List<DefaultRight>> GetDefaultRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);
             
            var sys = _registeredSystemsMockList.Find(r => r.SystemTypeId.Equals(systemId));

            List<DefaultRight> list = [];
            list.AddRange(sys.DefaultRights);

            return list;
        }

        /// <summary>
        /// The ClientId list is maintained to ensure uniqueness
        /// </summary>
        /// <param name="clientId">A Guid inserted by Idporten</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public Task<bool> CreateClient(string clientId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<Guid?> CreateRegisteredSystem(RegisteredSystem system, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }
    }
}
