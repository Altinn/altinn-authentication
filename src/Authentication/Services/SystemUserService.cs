using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Options;

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
        DelegationHelper delegationHelper,
        IPartiesClient partiesClient,
        IOptions<PaginationOptions> paginationOption) : ISystemUserService
    {
        private readonly ISystemUserRepository _repository = systemUserRepository;
        private readonly ISystemRegisterRepository _registerRepository = systemRegisterRepository;
        private readonly IAccessManagementClient _accessManagementClient = accessManagementClient;
        private readonly IPartiesClient _partiesClient = partiesClient;

        /// <summary>
        /// Used to limit the number of items returned in a paginated list
        /// </summary>
        private int _paginationSize = paginationOption.Value.Size;

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// </summary>
        /// <returns>The SystemUser created</returns>
        public async Task<SystemUser?> CreateSystemUser(string partyId, SystemUserRequestDto request, int userId)
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
                SystemInternalId = regSystem.InternalId,
                IntegrationTitle = request.IntegrationTitle,
                SystemId = request.SystemId,
                PartyId = partyId
            };

            Guid? insertedId = await _repository.InsertSystemUser(newSystemUser, userId);        
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
            RegisteredSystem? system = await _registerRepository.GetRegisteredSystemById(systemId);
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

            return Page.Create(theList, _paginationSize, static theList => theList.Id);
        }

        /// <inheritdoc/>
        public async Task<Result<CreateSystemUserResponse>> CreateAndDelegateSystemUser(string partyId, SystemUserRequestDto request, int userId, CancellationToken cancellationToken)
        {
            RegisteredSystem? regSystem = await _registerRepository.GetRegisteredSystemById(request.SystemId);
            if (regSystem is null)
            {
                return Problem.SystemIdNotFound;
            }

            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId), cancellationToken);

            if (party is null)
            {
                return Problem.Reportee_Orgno_NotFound;
            }
                        
            DelegationCheckResult delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(int.Parse(partyId), regSystem.Id, cancellationToken);
            if (delegationCheckFinalResult.RightResponses is null)
            {
                // This represents some problem with doing the delegation check beyond the rights not being delegable.
                return Problem.UnableToDoDelegationCheck;
            }

            if (!delegationCheckFinalResult.CanDelegate)
            {
                // This represents that the rights are not delegable, but the DelegationCheck method call has been completed.
                return new CreateSystemUserResponse
                {
                    IsSuccess = false,
                    SystemUser = null,
                    Problem = MapDetailExternalErrorListToProblemInstance(delegationCheckFinalResult.errors)
                };
            }

            SystemUser newSystemUser = new()
            {
                ReporteeOrgNo = party.OrgNumber,
                SystemInternalId = regSystem.InternalId,
                IntegrationTitle = request.IntegrationTitle,
                SystemId = request.SystemId,
                PartyId = partyId
            };

            Guid? insertedId = await _repository.InsertSystemUser(newSystemUser, userId);
            if (insertedId is null)
            {
                return Problem.SystemUser_FailedToCreate;
            }

            SystemUser? inserted = await _repository.GetSystemUserById((Guid)insertedId);
            if (inserted is null)
            {
                return Problem.SystemUser_FailedToCreate;
            }

            Result<bool> delegationSucceeded = await _accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), inserted, delegationCheckFinalResult.RightResponses);
            if (delegationSucceeded.IsProblem)
            {
                await _repository.SetDeleteSystemUserById((Guid)insertedId);
                return new CreateSystemUserResponse
                {
                    IsSuccess = false,
                    SystemUser = null,
                    Problem = delegationSucceeded.Problem
                };
            }

            return new CreateSystemUserResponse
            {
                IsSuccess = true,
                SystemUser = inserted
            };
        }

        private static ProblemInstance MapDetailExternalErrorListToProblemInstance(List<DetailExternal>? errors)
        {
            if (errors is null || errors.Count == 0 || errors[0].Code == DetailCodeExternal.Unknown)
            {
                return Problem.UnableToDoDelegationCheck;
            }

            if (errors[0].Code == DetailCodeExternal.MissingRoleAccess)
            {
                return Problem.DelegationRightMissingRoleAccess;
            }

            if (errors[0].Code == DetailCodeExternal.MissingDelegationAccess)
            {
                return Problem.DelegationRightMissingDelegationAccess;
            }

            if (errors[0].Code == DetailCodeExternal.MissingSrrRightAccess)
            {
                return Problem.DelegationRightMissingSrrRightAccess;
            }

            if (errors[0].Code == DetailCodeExternal.InsufficientAuthenticationLevel)
            {
                return Problem.DelegationRightInsufficientAuthenticationLevel;
            }

            return Problem.UnableToDoDelegationCheck;
        }
    }
}
