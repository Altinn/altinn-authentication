using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
#nullable enable
    /// <summary>
    /// CRUD API for the System User 
    /// </summary>
    ///[Authorize]
    [FeatureGate(FeatureFlags.SystemUser)]
    [Route("authentication/api/v1/systemuser")]
    [ApiController]
    public class SystemUserController : ControllerBase
    {
        private readonly ISystemUserService _systemUserService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="systemUserService">The SystemUserService supports this API specifically.</param>
        public SystemUserController(ISystemUserService systemUserService)
        {
            _systemUserService = systemUserService;
        }

        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{partyId}")]
        public async Task<ActionResult> GetListOfSystemUsersPartyHas(int partyId)
        {
            List<SystemUser>? theList = await _systemUserService.GetListOfSystemUsersPartyHas(partyId);

            if (theList is not null && theList.Count > 0)
            {
                return Ok(theList);
            }

            return NotFound();
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{partyId}/{systemUserId}")]
        public async Task<ActionResult> GetSingleSystemUserById(int partyId, Guid systemUserId)
        {
            SystemUser? systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
            if (systemUser is not null)
            {
                return Ok(systemUser);
            }

            return NotFound();
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{partyId}/{systemUserId}")]
        public async Task<ActionResult> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            SystemUser? toBeDeleted = await _systemUserService.GetSingleSystemUserById(systemUserId);
            if (toBeDeleted is not null)
            {
                await _systemUserService.SetDeleteFlagOnSystemUser(systemUserId);
                return Ok(1);
            }

            return NotFound(0);            
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns>        
        [Produces("application/json")]
        [ProducesResponseType(typeof(SystemUser), StatusCodes.Status201Created)]        
        [ProducesResponseType(StatusCodes.Status404NotFound)]        
        [HttpPost("{partyId}/{createRequestId}")]
        public async Task<ActionResult<SystemUser>> CreateSystemUser(SystemUser request)
        {
            SystemUser? toBeCreated = await _systemUserService.CreateSystemUser(request);
            if (toBeCreated is not null)
            {
                return Ok(toBeCreated);
            }

            return NotFound();
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{partyId}/{systemUserId}")]
        public async Task<ActionResult> UpdateSystemUserById(SystemUser request)
        {
            SystemUser? toBeUpdated = await _systemUserService.GetSingleSystemUserById(Guid.Parse(request.Id));
            if (toBeUpdated is not null)
            {
                await _systemUserService.UpdateSystemUserById(Guid.Parse(request.Id), request);
                return Ok();
            }

            return NotFound();
        }
    }    
}
