using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;

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
    }
}
