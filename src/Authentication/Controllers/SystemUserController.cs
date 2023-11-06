using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("list/{partyId}")]
        public async Task<ActionResult> GetListOfSystemUsersPartyHas()
        {
            await Task.Delay(10);
            return Ok();
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> GetSingleSystemUserById()
        {
            await Task.Delay(20);
            return Ok();
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> SetDeleteFlagOnSystemUser()
        {
            await Task.Delay(30);
            return Ok();
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
        [HttpPost("systemuser/{partyId}/{createRequestId}")]
        public async Task<ActionResult<SystemUserResponse>> CreateSystemUser()
        {
            await Task.Delay(40);
            return Ok();
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Consumes("application/x-www-form-urlencoded")]
        [HttpPut("systemuser/{partyId}/{systemUserId}")]
        public async Task<ActionResult> UpdateSystemUserById()
        {
            await Task.Delay(50);
            return Ok();
        }
    }    
}
