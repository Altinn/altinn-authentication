using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Services.Interfaces;

#nullable enable
namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemUserService : ISystemUserService
    {
        private readonly ISystemUserRepository _repository;
        private readonly ISystemRegisterRepository _registerRepository;

        /// <summary>
        /// The Constructor
        /// </summary>
        public SystemUserService(ISystemUserRepository systemUserRepository, ISystemRegisterRepository systemRegisterRepository)
        {            
            _repository = systemUserRepository;
            _registerRepository = systemRegisterRepository;
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// </summary>
        /// <returns>The SystemUser created</returns>
        public async Task<SystemUser?> CreateSystemUser(SystemUserRequestDto request, string partyOrgNo)
        {
            RegisterSystemResponse? regSystem = await _registerRepository.GetRegisteredSystemById(request.SystemId);
            if (regSystem == null)
            {
                return null;
            }

            SystemUser newSystemUser = new()
            {                
                ReporteeOrgNo = partyOrgNo,
                SystemInternalId = regSystem.SystemInternalId,
                IntegrationTitle = request.IntegrationTitle,
                SystemId = request.SystemId,
                PartyId = request.PartyId.ToString()
            };

            Guid? insertedId = await _repository.InsertSystemUser(newSystemUser);        
            if (insertedId == null)
            {
                return null;
            }

            SystemUser? inserted = await _repository.GetSystemUserById((Guid)insertedId);
            return inserted;
        }

        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered.
        /// </summary>
        /// <returns>list of SystemUsers</returns>
        public async Task<List<SystemUser>> GetListOfSystemUsersForParty(int partyId)
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
        /// <returns>SystemUser</returns>
        public async Task<SystemUser> GetSingleSystemUserById(Guid systemUserId)
        {
            SystemUser search = await _repository.GetSystemUserById(systemUserId);
            
            return search;
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns>Boolean True if row affected</returns>
        public async Task<bool> SetDeleteFlagOnSystemUser(Guid systemUserId)

        {
            SystemUser toBeDeleted = await _repository.GetSystemUserById(systemUserId);
            if (toBeDeleted != null) 
            {
                await _repository.SetDeleteSystemUserById(systemUserId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public async Task<int> UpdateSystemUserById(SystemUserUpdateDto request)
        {
            SystemUser search = await _repository.GetSystemUserById(Guid.Parse(request.Id));
            if (search == null)
            {                
                return 0;
            }

            if (request.SystemId == null )
            {
                return 0;
            }

            return await _repository.UpdateProductName(Guid.Parse(request.Id), request.SystemId);
        }

        /// <inheritdoc/>
        public async Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, CancellationToken cancellationToken)
        {
            return await _repository.CheckIfPartyHasIntegration(clientId, systemProviderOrgNo, systemUserOwnerOrgNo, cancellationToken);
        }
    }
}
