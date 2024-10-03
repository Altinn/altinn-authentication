using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;

#nullable enable
namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    /// <remarks>
    /// The Constructor
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class SystemUserService(
        ISystemUserRepository systemUserRepository,
        ISystemRegisterRepository systemRegisterRepository,
        IAccessManagementClient accessManagementClient,
        IPartiesClient partiesClient) : ISystemUserService
    {
        private readonly ISystemUserRepository _repository = systemUserRepository;
        private readonly ISystemRegisterRepository _registerRepository = systemRegisterRepository;
        private readonly IAccessManagementClient _accessManagementClient = accessManagementClient;
        private readonly IPartiesClient _partiesClient = partiesClient;

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// </summary>
        /// <returns>The SystemUser created</returns>
        public async Task<SystemUser?> CreateSystemUser(string partyId, SystemUserRequestDto request)
        {
            RegisteredSystem? regSystem = await _registerRepository.GetRegisteredSystemById(request.SystemId);
            if (regSystem is null)
            {
                return null;
            }

            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId));
           
            if (party is null)
            {
                return null;
            }

            SystemUser newSystemUser = new()
            {                
                ReporteeOrgNo = party.OrgNumber,
                SystemInternalId = regSystem.SystemInternalId,
                IntegrationTitle = request.IntegrationTitle,
                SystemId = request.SystemId,
                PartyId = partyId
            };

            Guid? insertedId = await _repository.InsertSystemUser(newSystemUser);        
            if (insertedId is null)
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
                return [];
            }

            return await _repository.GetAllActiveSystemUsersForParty(partyId);
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns>SystemUser</returns>
        public async Task<SystemUser?> GetSingleSystemUserById(Guid systemUserId)
        {
            SystemUser? search = await _repository.GetSystemUserById(systemUserId);
            
            return search;
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns>Boolean True if row affected</returns>
        public async Task<bool> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            await _repository.SetDeleteSystemUserById(systemUserId);            

            return true; // if it can't be found, there is no need to delete it.
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

            return await _repository.UpdateIntegrationTitle(Guid.Parse(request.Id), request.SystemId);
        }

        /// <inheritdoc/>
        public async Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, CancellationToken cancellationToken)
        {
            return await _repository.CheckIfPartyHasIntegration(clientId, systemProviderOrgNo, systemUserOwnerOrgNo, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Result<Page<SystemUser, string>>> GetAllSystemUsersByVendorSystem(
            OrganisationNumber vendorOrgNo, 
            string systemId, 
            Page<string>.Request continueRequest, 
            CancellationToken cancellationToken)
        {
            RegisterSystemResponse? system = await systemRegisterRepository.GetRegisteredSystemById(systemId);
            if (system is null)
            {
                return Problem.SystemIdNotFound;
            }

            // Verify that the orgno from the logged on token owns this system
            if (OrganisationNumber.CreateFromStringOrgNo(system.SystemVendorOrgNumber) != vendorOrgNo)
            {
                return Problem.SystemIdNotFound;
            }

            List<SystemUser>? theList = await _repository.GetAllSystemUsersByVendorSystem(systemId, cancellationToken);
            theList ??= [];

            return Page.Create(theList, 3, static theList => theList.Id);
        }
    }
}
