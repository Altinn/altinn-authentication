using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemUserService : ISystemUserService
    {
        private readonly ISystemUserRepository _repository;

        /// <summary>
        /// The Constructor
        /// </summary>
        public SystemUserService(ISystemUserRepository systemUserRepository)
        {            
            _repository = systemUserRepository;
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// </summary>
        /// <returns>The SystemUser created</returns>
        public async Task<SystemUser> CreateSystemUser(SystemUserRequestDto request, int partyId)
        {
            SystemUser newSystemUser = new()
            {                
                IntegrationTitle = request.IntegrationTitle,
                ProductName = request.ProductName,
                OwnedByPartyId = partyId.ToString()
            };

            Guid insertedId = await _repository.InsertSystemUser(newSystemUser);            
            SystemUser inserted = await _repository.GetSystemUserById(insertedId);
            return inserted;
        }

        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered.
        /// </summary>
        /// <returns></returns>
        public async Task<List<SystemUser>> GetListOfSystemUsersPartyHas(int partyId)
        {
            if (partyId < 1)
            {
                return new List<SystemUser>(null);
            }

            return await _repository.GetAllActiveSystemUsersForParty(partyId);
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        public async Task<SystemUser> GetSingleSystemUserById(Guid systemUserId)
        {
            SystemUser search = await _repository.GetSystemUserById(systemUserId);
            
            return search;
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        public async Task<int> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            SystemUser toBeDeleted = await _repository.GetSystemUserById(systemUserId);
            if (toBeDeleted != null) 
            {
                await _repository.SetDeleteSystemUserById(systemUserId);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        public async Task<int> UpdateSystemUserById(SystemUserUpdateDto request)
        {
            SystemUser search = await _repository.GetSystemUserById(Guid.Parse(request.Id));
            if (search == null)
            {                
                return 0;
            }

            if (request.ProductName == null )
            {
                return 0;
            }

            return await _repository.UpdateProductName(Guid.Parse(request.Id), request.ProductName);
        }
    }
}
