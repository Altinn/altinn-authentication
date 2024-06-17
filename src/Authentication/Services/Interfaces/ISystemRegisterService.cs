using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// The Interface for the System Register Service
    /// </summary>
    public interface ISystemRegisterService
    {
        /// <summary>
        /// Retrieves the list of all Registered Systems, except those marked as deleted.
        /// </summary>
        /// <param name="cancellation">The cancellation token</param>
        /// <returns></returns>
        Task<List<RegisterSystemResponse>> GetListRegSys(CancellationToken cancellation = default);

        /// <summary>
        /// Retrieves the list, if any, of the Default Rights the System Provider
        /// has set for the Registered System.
        /// </summary>
        /// <param name="systemId">The Id of the Registered System</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>List of Default Rights</returns>
        Task<List<Right>> GetRightsForRegisteredSystem(string systemId, CancellationToken cancellation = default);

        /// <summary>
        /// Inserts a new unique ClientId
        /// </summary>
        /// <param name="clientId">The Client_Ids are maintained by Maskinporten, but we need to reference them in the db</param>
        /// <param name="cancellationToken">The Cancellationtoken</param>
        /// <returns></returns>
        Task<bool> CreateClient(string clientId, CancellationToken cancellationToken);

        /// <summary>
        /// Inserts a new Registered System
        /// </summary>
        /// <param name="system">The descriptor DTO for a new System</param>
        /// <param name="cancellation">The Cancelation token</param>
        /// <returns></returns>
        Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest system, CancellationToken cancellation = default);
    }
}
