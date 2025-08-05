using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller for handling system change logs.
    /// </summary>
    [Route("authentication/api/v1/systemchangelog")]
    [ApiController]
    public class SystemChangeLogController : ControllerBase
    {
        private readonly ISystemChangeLogService _systemChangeLogService;
        private readonly ISystemRegisterService _systemRegisterService;

        /// <summary>
        /// Constructor for the SystemChangeLogController class.
        /// </summary>
        /// <param name="systemChangeLogService">handler for the system change log service</param>
        public SystemChangeLogController(ISystemChangeLogService systemChangeLogService, ISystemRegisterService systemRegisterService)
        {
            this._systemChangeLogService = systemChangeLogService;
            this._systemRegisterService = systemRegisterService;
        }

        /// <summary>
        /// Gets the change log for a specific system identified by its internal ID.
        /// </summary>
        /// <param name="systemId">the system internal id</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        [HttpGet("{systemId}")]
        [Authorize(Policy = AuthzConstants.POLICY_SCOPE_SYSTEMREGISTER_WRITE)]
        public async Task<ActionResult<List<SystemChangeLog>>> GetChangeLogAsync(string systemId, CancellationToken cancellationToken = default)
        {
            RegisteredSystemResponse registeredSystem = await _systemRegisterService.GetRegisteredSystemInfo(systemId, cancellationToken);
            if (!AuthenticationHelper.HasWriteAccess(AuthenticationHelper.GetOrgNumber(registeredSystem.Vendor.ID), User))
            {
                return Forbid();
            }

            if (registeredSystem is null)
            {
                return NotFound($"System with ID {systemId} not found.");
            }
            else
            {
                if (registeredSystem.IsDeleted)
                {
                    return NotFound($"System with ID {systemId} is deleted.");
                }
            }

            Guid systemInternalId = registeredSystem.InternalId;
            var changeLog = await _systemChangeLogService.GetChangeLogAsync(systemInternalId, cancellationToken);
            return Ok(changeLog);
        }
    }
}
