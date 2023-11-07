using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    public class SystemUserServiceMock : ISystemUserService
    {
        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns>
        public Task<SystemUser> CreateSystemUser(SystemUser request)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered
        /// </summary>
        /// <returns></returns>
        public Task<List<SystemUser>> GetListOfSystemUsersPartyHas(int partyId)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        public Task<SystemUser> GetSingleSystemUserById(Guid systemUserId)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        public Task<int> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        public Task UpdateSystemUserById(Guid systemUserId)
        {
            throw new System.NotImplementedException();
        }
    }
}
