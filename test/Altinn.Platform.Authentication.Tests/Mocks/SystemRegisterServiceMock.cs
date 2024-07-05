using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        private readonly List<RegisterSystemResponse> _registeredSystemsMockList;

        public SystemRegisterServiceMock()
        {
            _registeredSystemsMockList = MockDataHelper();
        }

        public async Task<List<RegisterSystemResponse>> GetListRegSys(CancellationToken cancellation = default)
        {
            await Task.Delay(50);
            return _registeredSystemsMockList;
        }
                
        private static List<RegisterSystemResponse> MockDataHelper() 
        {
            List<string> clientId = new List<string>();
            clientId.Add("96ea3185-23cc-4df5-88f3-d43fbd995f34");

            RegisterSystemResponse reg1 = new()
            {
                SystemVendorOrgName = "Test Org AS",
                SystemId = "Awesome_Tax",
                SystemName = "Awesome_Tax",
                Rights =
                [
                    new()
                    {
                        Resources =
                        [
                            new AttributePair 
                            { 
                                Id = "urn:altinn:resource", 
                                Value = "mva"
                            }
                        ]
                    },
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "mva"
                            }
                        ]
                    }
                ]
            };

            RegisterSystemResponse reg2 = new()
            {
                SystemVendorOrgName = "Wonderful",
                SystemId = "Wonderful_Tax",
                SystemName = "Wonderful_Tax",
                Rights =
                [
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "mva"
                            }
                        ]
                    },
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "mva"
                            }
                        ]
                    }
                ]
            };

            RegisterSystemResponse reg3 = new()
            {
                SystemId = "Brilliant_HR",
                SystemName = "Brilliant HR",
                SystemVendorOrgNumber = "914286018",
                ClientId = clientId,
                Rights =
                [
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "kravogbetaling"
                            }
                        ]
                    },
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "lonn"
                            }
                        ]
                    }
                ]
            };

            RegisterSystemResponse reg4 = new()
            {
                SystemId = "Fantastic_HR",
                SystemName = "Fantastic HR",
                Rights =
                [
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "kravogbetaling"
                            }
                        ]
                    },
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "lonn"
                            }
                        ]
                    }
                ]
            };

            RegisterSystemResponse reg5 = new()
            {
                SystemId = "business_next",
                SystemName = "Business Next",
                SystemVendorOrgNumber = "914286018",
                ClientId = clientId,
                Rights =
                [
                    new()
                    {
                        Resources =
                        [
                            new AttributePair
                            {
                                Id = "urn:altinn:resource",
                                Value = "kravogbetaling"
                            }
                        ]
                    }
                ]
            };

            List<RegisterSystemResponse> list =
                [
                    reg1, reg2, reg3, reg4, reg5
                ];

            return list;
        }

        public async Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);
             
            var sys = _registeredSystemsMockList.Find(r => r.SystemId.Equals(systemId));

            List<Right> list = [];
            list.AddRange(sys.Rights);

            return list;
        }

        /// <summary>
        /// The ClientId list is maintained to ensure uniqueness
        /// </summary>
        /// <param name="clientId">A Guid inserted by Idporten</param>
        /// <param name="system_internal_id">the system internal id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public Task<bool> CreateClient(string clientId, Guid system_internal_id, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public async Task<RegisterSystemResponse> GetRegisteredSystemInfo(string systemId, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);

            RegisterSystemResponse registeredSystem = _registeredSystemsMockList.Find(r => r.SystemId.Equals(systemId));

            return registeredSystem;
        }

        public async Task<bool> DoesClientIdExists(List<string> clientId, CancellationToken cancellationToken)
        {
            List<RegisterSystemResponse> result = _registeredSystemsMockList.FindAll(r => r.ClientId.Intersect(clientId).Any());
            return result.Count() >= 1;
        }
    }
}
