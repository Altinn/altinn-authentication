using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// CRUD API for the System User 
    /// </summary>
    //[Authorize]
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
        [HttpGet("list/{partyId}")]
        public async Task<ActionResult> GetListOfSystemUsersPartyHas(int partyId)
        {
            List<SystemUserResponse> theList = await _systemUserService.GetListOfSystemUsersPartyHas(partyId);

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
        [HttpGet("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> GetSingleSystemUserById(Guid systemUserId)
        {
            SystemUserResponse systemUser = await _systemUserService.GetSingleSystemUserById(systemUserId);
            if (systemUser is not null)
            {
                return Ok();
            }

            return NotFound();
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            SystemUserResponse toBeDeleted = await _systemUserService.GetSingleSystemUserById(systemUserId);
            if (toBeDeleted is not null)
            {
                await _systemUserService.SetDeleteFlagOnSystemUser(systemUserId);
                return Ok();
            }

            return NotFound();            
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns>        
        [Produces("application/json")]
        [ProducesResponseType(typeof(SystemUserResponse), StatusCodes.Status201Created)]        
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Consumes("application/x-www-form-urlencoded")]
        [HttpPost("systemuser/{partyId}/{createRequestId}")]
        public async Task<ActionResult<SystemUserResponse>> CreateSystemUser([FromBody] SystemUserCreateRequest request)
        {
            SystemUserResponse toBeCreated = await _systemUserService.CreateSystemUser(request);
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
        [Consumes("application/x-www-form-urlencoded")]
        [HttpPut("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> UpdateSystemUserById([FromBody] SystemUserCreateRequest request)
        {
            SystemUserResponse toBeUpdated = await _systemUserService.GetSingleSystemUserById(Guid.Parse(request.Id));
            if (toBeUpdated is not null)
            {
                await _systemUserService.UpdateSystemUserById(Guid.Parse(request.Id));
                return Ok();
            }

            return NotFound();
        }
    }    
}
