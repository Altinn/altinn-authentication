using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Enums;
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
using Microsoft.FeatureManagement;
using Newtonsoft.Json.Linq;

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
        ISystemRegisterService systemRegisterService,
        IAccessManagementClient accessManagementClient,
        DelegationHelper delegationHelper,
        IPartiesClient partiesClient,
        IOptions<PaginationOptions> paginationOption) : ISystemUserService
    {
        private readonly ISystemUserRepository _repository = systemUserRepository;
        private readonly ISystemRegisterRepository _registerRepository = systemRegisterRepository;
        private readonly ISystemRegisterService systemRegisterService = systemRegisterService;
        private readonly IAccessManagementClient _accessManagementClient = accessManagementClient;
        private readonly IPartiesClient _partiesClient = partiesClient;

        /// <summary>
        /// Used to limit the number of items returned in a paginated list
        /// </summary>
        private int _paginationSize = paginationOption.Value.Size;

        /// <summary>
        /// Used to set the stream chunk limit, for the internal API
        /// </summary>
        const int STREAM_LIMIT = 10;

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// </summary>
        /// <returns>The SystemUser created</returns>    
        public async Task<Result<SystemUser>> CreateSystemUser(string partyId, SystemUserRequestDto request, int userId)
        {
            RegisteredSystemResponse? regSystem = await _registerRepository.GetRegisteredSystemById(request.SystemId);
            if (regSystem is null)
            {
                return Problem.SystemIdNotFound;
            }

            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId));
       
            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.SystemUserNotFound;
            }

            ExternalRequestId externalRequestId = new()
            {
                OrgNo = party.OrgNumber,
                SystemId = request.SystemId,
                ExternalRef = party.OrgNumber // This is the fallback if no ExternalRef is provided, and in L1 this is the same as the OrgNo
            };

            SystemUser? existing = await _repository.GetSystemUserByExternalRequestId(externalRequestId);
            if (existing is not null)
            {
                return Problem.SystemUser_AlreadyExists;
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

        /// <inheritdoc/>
        public async Task<List<SystemUser>> GetListOfAgentSystemUsersForParty(int partyId)
        {
            if (partyId < 1)
            {
                return [];
            }

            return await _repository.GetAllActiveAgentSystemUsersForParty(partyId);
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
        public async Task<Result<bool>> SetDeleteFlagOnSystemUser(string partyId, Guid systemUserId, CancellationToken cancellationToken = default)
        {
            SystemUser? systemUser = await _repository.GetSystemUserById(systemUserId);
            if (systemUser is null) 
            {
                return Problem.SystemUserNotFound;   
            }

            if (systemUser.PartyId != partyId)
            {
                return Problem.Delete_SystemUser_NotOwned;
            }

            if (systemUser.UserType != Core.Enums.SystemUserType.Standard)
            {
                return Problem.AgentSystemUser_ExpectedStandardUserType;
            }

            await _repository.SetDeleteSystemUserById(systemUserId);

            List<Right> rights = await systemRegisterService.GetRightsForRegisteredSystem(systemUser.SystemId, cancellationToken);

            foreach (Right right in rights)
            {
                List<AttributePair> resource = DelegationHelper.ConvertAppResourceToOldResourceFormat(right.Resource);

                right.Resource = resource;
            }

            await _accessManagementClient.RevokeDelegatedRightToSystemUser(partyId, systemUser, rights);
            return true; // if it can't be found, there is no need to delete it.
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public async Task<int> UpdateSystemUserById(SystemUserUpdateDto request)
        {
            SystemUser? search = await _repository.GetSystemUserById(Guid.Parse(request.Id));
            if (search == null)
            {                
                return 0;
            }

            if (request.SystemId == null)
            {
                return 0;
            }

            return await _repository.UpdateIntegrationTitle(Guid.Parse(request.Id), request.SystemId);
        }

        /// <inheritdoc/>
        public async Task<SystemUser?> CheckIfPartyHasIntegration(
            string clientId, 
            string systemProviderOrgNo, 
            string systemUserOwnerOrgNo, 
            string externalRef,
            CancellationToken cancellationToken)
        {
            return await _repository.CheckIfPartyHasIntegration(
                clientId, 
                systemProviderOrgNo, 
                systemUserOwnerOrgNo, 
                externalRef,
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Result<Page<SystemUser, long>>> GetAllSystemUsersByVendorSystem(
            OrganisationNumber vendorOrgNo, 
            string systemId, 
            Page<long>.Request continueRequest, 
            CancellationToken cancellationToken)
        {
            RegisteredSystemResponse? system = await _registerRepository.GetRegisteredSystemById(systemId);
            if (system is null)
            {
                return Problem.SystemIdNotFound;
            }

            // Verify that the orgno from the logged on token owns this system
            if (OrganisationNumber.CreateFromStringOrgNo(system.SystemVendorOrgNumber) != vendorOrgNo)
            {
                return Problem.SystemIdNotFound;
            }

            long continueFrom = 0;
            if (continueRequest is not null && continueRequest.ContinuationToken > 0)
            {
                continueFrom = continueRequest.ContinuationToken;
            }

            List<SystemUser>? theList = await _repository.GetAllSystemUsersByVendorSystem(systemId, continueFrom, _paginationSize, cancellationToken);
            theList ??= [];

            return Page.Create(theList, _paginationSize, static theList => theList.SequenceNo);
        }

        /// <inheritdoc/>
        public async Task<Result<SystemUser>> CreateAndDelegateSystemUser(string partyId, SystemUserRequestDto request, int userId, CancellationToken cancellationToken)
        {
            RegisteredSystemResponse? regSystem = await _registerRepository.GetRegisteredSystemById(request.SystemId);
            if (regSystem is null)
            {
                return Problem.SystemIdNotFound;
            }

            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId), cancellationToken);

            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.Reportee_Orgno_NotFound;
            }

            ExternalRequestId externalRequestId = new()
            {
                OrgNo = party.OrgNumber,
                SystemId = request.SystemId,
                ExternalRef = party.OrgNumber // This is the fallback if no ExternalRef is provided, and in L1 this is the same as the OrgNo
            };

            SystemUser? existing = await _repository.GetSystemUserByExternalRequestId(externalRequestId);
            if (existing is not null)
            {
                return Problem.SystemUser_AlreadyExists;
            }

            DelegationCheckResult delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(int.Parse(partyId), regSystem.Id, [], true, cancellationToken);
            if (delegationCheckFinalResult.RightResponses is null)
            {
                // This represents some problem with doing the delegation check beyond the rights not being delegable.
                return Problem.UnableToDoDelegationCheck;
            }

            if (!delegationCheckFinalResult.CanDelegate)
            {
                // This represents that the rights are not delegable, but the DelegationCheck method call has been completed.
                return DelegationHelper.MapDetailExternalErrorListToProblemInstance(delegationCheckFinalResult.errors);
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
                return delegationSucceeded.Problem;
            }

            return inserted;
        }

        /// <inheritdoc/>
        public async Task<SystemUser?> GetSystemUserByExternalRequestId(ExternalRequestId externalRequestId)
        {
            return await _repository.GetSystemUserByExternalRequestId(externalRequestId);
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<SystemUserRegisterDTO>>> GetAllSystemUsers(long continueFrom, CancellationToken cancellationToken)
        {
            List<SystemUserRegisterDTO>? systemUserDtos = await _repository.GetAllSystemUsers(continueFrom, STREAM_LIMIT, cancellationToken);
            systemUserDtos ??= [];
            return systemUserDtos;
        }

        /// <inheritdoc/>
        public async Task<long> GetMaxSystemUserSequenceNo()
        {
            return await _repository.GetMaxSystemUserSequenceNo();
        }

        /// <inheritdoc/>
        public async Task<Result<List<DelegationResponse>>> DelegateToAgentSystemUser(SystemUser systemUser, AgentDelegationInputDto request, int userId, IFeatureManager featureManager, CancellationToken cancellationToken)
        {
            bool mockCustomerApi = await featureManager.IsEnabledAsync(FeatureFlags.MockCustomerApi);
            Result<List<AgentDelegationResponse>> result = await _accessManagementClient.DelegateCustomerToAgentSystemUser(systemUser, request, userId, mockCustomerApi, cancellationToken);
            if (result.IsSuccess)
            {
                List<DelegationResponse> theList = [];

                foreach (var item in result.Value)
                {
                    var newDel = new DelegationResponse()
                    {
                        DelegationId = item.DelegationId,
                        CustomerId = item.FromEntityId,
                        AgentSystemUserId = (Guid)systemUser.SystemInternalId!
                    };

                    theList.Add(newDel);
                }

                return theList;
            }

            return result.Problem;
        }

        /// <inheritdoc/>
        public async Task<Result<List<DelegationResponse>>> GetListOfDelegationsForAgentSystemUser(int partyId, Guid facilitator, Guid systemUserId)
        {
            Party party = await _partiesClient.GetPartyAsync(partyId);

            if (party.PartyUuid != facilitator)
            {
                return Problem.AgentSystemUser_DelegationNotFound;
            }

            var res = await _accessManagementClient.GetDelegationsForAgent(systemUserId, facilitator);
            if (res.IsSuccess)
            {
                return ConvertExtDelegationToDTO(res.Value);
            }

            return res.Problem ?? Problem.UnableToDoDelegationCheck;
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteClientDelegationToAgentSystemUser(string partyId, Guid delegationId, Guid partyUUId, CancellationToken cancellationToken = default)
        {
            Result<bool> result = await _accessManagementClient.DeleteCustomerDelegationToAgent(partyUUId, delegationId, cancellationToken);
            if (result.IsProblem)
            {
                return result.Problem;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteAgentSystemUser(string partyId, Guid systemUserId, Guid facilitatorId, CancellationToken cancellationToken = default)
        {
            SystemUser? systemUser = await _repository.GetSystemUserById(systemUserId);
            if (systemUser is null)
            {
                return Problem.SystemUserNotFound;
            }

            if (systemUser.PartyId != partyId)
            {
                return Problem.Delete_SystemUser_NotOwned;
            }

            if (systemUser.UserType != Core.Enums.SystemUserType.Agent)
            {
                return Problem.AgentSystemUser_ExpectedAgentUserType;
            }

            Result<List<ConnectionDto>> delegations = await _accessManagementClient.GetDelegationsForAgent(systemUserId, facilitatorId);
            if (delegations.IsSuccess && delegations.Value.Count > 0)
            {
                return Problem.AgentSystemUser_HasDelegations;
            }
            else
            {
                Result<bool> result = await _accessManagementClient.DeleteSystemUserAssignment(facilitatorId, systemUserId, cancellationToken);
                if (result.IsProblem)
                {
                    if (result.Problem.Detail == Problem.AgentSystemUser_AssignmentNotFound.Detail)
                    {
                        await _repository.SetDeleteSystemUserById(systemUserId);
                        return true;
                    }
                    else
                    {
                        return result.Problem;
                    }                    
                }

                await _repository.SetDeleteSystemUserById(systemUserId);
                return true;
            }
        }

        /// <inheritdoc/>
        public async Task<Result<List<Customer>>> GetClientsForFacilitator(Guid facilitator, List<string> packages, CustomerRoleType customerRoleType, IFeatureManager featureManager, CancellationToken cancellationToken)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.MockCustomerApi))
            {
                var res = await _partiesClient.GetPartyCustomers(facilitator, customerRoleType, cancellationToken);
                if (res.IsSuccess)
                {
                    return ConvertPartyCustomerToClient(res.Value);
                }

                return res.Problem ?? Problem.AgentSystemUser_FailedToGetClients;
            }
            else
            {
                var res = await _accessManagementClient.GetClientsForFacilitator(facilitator, packages, cancellationToken);
                if (res.IsSuccess)
                {
                    return ConvertConnectionDTOToClient(res.Value);
                }

                return res.Problem ?? Problem.AgentSystemUser_FailedToGetClients;
            }
        }

        private static Result<List<DelegationResponse>> ConvertExtDelegationToDTO(List<ConnectionDto> value)
        {
            List<DelegationResponse> result = [];

            foreach (var item in value)
            {
                var newDel = new DelegationResponse()
                {
                    AgentSystemUserId = item.To.Id,
                    DelegationId = item.Id,
                    CustomerId = item.From.Id,
                    AssignmentId = item.Delegation.ToId
                };

                result.Add(newDel);
            }

            return result;
        }

        private static Result<List<Customer>> ConvertConnectionDTOToClient(List<ClientDto> value)
        {
            List<Customer> result = [];
            foreach (var item in value)
            {
                var newCustomer = new Customer()
                {
                    DisplayName = item.Party.Name,
                    OrganizationIdentifier = item.Party.OrganizationNumber,
                    PartyUuid = item.Party.Id,
                    Access = item.Access
                };
                result.Add(newCustomer);
            }

            return result;
        }

        private static Result<List<Customer>> ConvertPartyCustomerToClient(CustomerList value)
        {
            List<Customer> result = [];
            foreach (var item in value.Data)
            {
                var newCustomer = new Customer()
                {
                    DisplayName = item.DisplayName,
                    OrganizationIdentifier = item.OrganizationIdentifier,
                    PartyUuid = item.PartyUuid,
                };
                result.Add(newCustomer);
            }

            return result;
        }
    }
}
