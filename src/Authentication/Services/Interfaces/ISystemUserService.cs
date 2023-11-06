using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// The service that supports the System User CRUD APIcontroller
    /// </summary>
    public interface ISystemUserService
    {
        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered
        /// </summary>
        /// <returns></returns>
        Task<List<SystemUserResponse>> GetListOfSystemUsersPartyHas();

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        Task<SystemUserResponse> GetSingleSystemUserById();

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        Task SetDeleteFlagOnSystemUser();

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns> 
        Task<SystemUserResponse> CreateSystemUser();

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        Task UpdateSystemUserById();
    }
}
