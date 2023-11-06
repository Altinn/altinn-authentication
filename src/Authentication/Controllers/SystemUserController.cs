﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// CRUD API for the System User 
    /// </summary>
    [Authorize]
    [Route("authentication/api/v1/systemuser")]
    [ApiController]
    public class SystemUserController : ControllerBase
    {
        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered
        /// </summary>
        /// <returns></returns>
        [HttpGet("list/{partyId}")]
        public async Task<ActionResult> GetListOfSystemUsersPartyHas()
        {
            await Task.Delay(50);
            return Ok();
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        [HttpGet("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> GetSingleSystemUserById()
        {
            await Task.Delay(50);
            return Ok();
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        [HttpDelete("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> SetDeleteFlagOnSystemUser()
        {
            await Task.Delay(50);
            return Ok();
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns>
        [HttpPost("systemuser/{partyId}/{createRequestId}")]
        public async Task<ActionResult> CreateSystemUser()
        {
            await Task.Delay(50);
            return Ok();
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        [HttpPut("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> UpdateSystemUserById()
        {
            await Task.Delay(50);
            return Ok();
        }
    }    
}
