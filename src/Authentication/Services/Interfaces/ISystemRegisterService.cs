using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// The Interface for the System Register Service
    /// </summary>
    public interface ISystemRegisterService
    {
        /// <summary>
        /// Retrieves the list of all Registered Systems
        /// </summary>
        /// <param name="cancellation">The cancellation token</param>
        /// <returns></returns>
        Task<List<RegisteredSystem>> GetListRegSys(CancellationToken cancellation = default);

        /// <summary>
        /// Retrieves the list, if any, of the Default Rights the System Provider
        /// has set for the Registered System.
        /// </summary>
        /// <param name="systemId">The Id of the Registered System</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>List of Default Rights</returns>
        Task<List<DefaultRights>> GetDefaultRightsForRegisteredSystem(Guid systemId, CancellationToken cancellation = default);
    }
}
