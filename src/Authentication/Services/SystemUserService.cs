using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Core.Problems;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Newtonsoft.Json.Linq;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

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
        const int STREAM_LIMIT = 100;

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
            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId), cancellationToken);

            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.Reportee_Orgno_NotFound;
            }

            if (!party.PartyUuid.HasValue)
            {
                return Problem.Party_PartyUuid_NotFound;
            }

            Guid partyUuid = party.PartyUuid.Value;

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

            List<Right> rights = await systemRegisterService.GetRightsForRegisteredSystem(systemUser.SystemId, cancellationToken);
            List<AccessPackage> accesssPackages = await systemRegisterService.GetAccessPackagesForRegisteredSystem(systemUser.SystemId, cancellationToken);
            bool isRightsDeleted = false;
            bool isAccessPackagesDeleted = false;
            if (rights.Count > 0)
            {
                foreach (Right right in rights)
                {
                    List<AttributePair> resource = DelegationHelper.ConvertAppResourceToOldResourceFormat(right.Resource);

                    right.Resource = resource;
                }

                var revokeRightResult = await _accessManagementClient.RevokeDelegatedRightToSystemUser(partyId, systemUser, rights);
                if (revokeRightResult.IsProblem)
                {
                    return revokeRightResult.Problem;
                }

                isRightsDeleted = revokeRightResult.Value;
            }

            var delegatedPackages = await GetAccessPackagesForSystemUser(partyUuid, new Guid(systemUser.Id), cancellationToken);
            if (delegatedPackages.IsProblem)
            {
                return Problem.AccessPackage_FailedToGetDelegatedPackages;
            }

            List<AccessPackage> accessPackagesForSystemUser = delegatedPackages.Value;

            if (accessPackagesForSystemUser.Count > 0)
            {
                var removeSystemUserResult = await _accessManagementClient.RemoveSystemUserAsRightHolder(partyUuid, systemUserId, true, cancellationToken);
                if (removeSystemUserResult.IsProblem)
                {
                    return removeSystemUserResult.Problem;
                }

                isAccessPackagesDeleted = removeSystemUserResult.Value;
            }

            if ((rights.Count > 0 && !isRightsDeleted) || (accessPackagesForSystemUser.Count > 0 && !isAccessPackagesDeleted))
            {
                return Problem.SystemUser_FailedToDelete;
            }

            await _repository.SetDeleteSystemUserById(systemUserId);
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
            DelegationCheckResult? delegationCheckFinalResult = null;
            AccessPackageDelegationCheckResult? accessPackageDelegationCheckResult = null;

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

            if (!party.PartyUuid.HasValue)
            {
                return Problem.Party_PartyUuid_NotFound;
            }

            Guid partyUuid = party.PartyUuid.Value;

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

            if (regSystem.Rights is not null && regSystem.Rights.Count > 0)
            {
                delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(int.Parse(partyId), regSystem.Id, [], true, cancellationToken);

                if (delegationCheckFinalResult?.RightResponses is null)
                {
                    // This represents some problem with doing the delegation check beyond the rights not being delegable.
                    return Problem.UnableToDoDelegationCheck;
                }

                if (!delegationCheckFinalResult.CanDelegate)
                {
                    // This represents that the rights are not delegable, but the DelegationCheck method call has been completed.
                    return DelegationHelper.MapDetailExternalErrorListToProblemInstance(delegationCheckFinalResult.errors);
                }
            }

            if (regSystem.AccessPackages is not null && regSystem.AccessPackages.Count > 0)
            {
                var accessPackageCheckResult = await delegationHelper.ValidateDelegationRightsForAccessPackages(partyUuid, regSystem.Id, regSystem.AccessPackages, true, cancellationToken);
                if (accessPackageCheckResult.IsProblem)
                {
                    return accessPackageCheckResult.Problem;
                }
                else
                {
                    accessPackageDelegationCheckResult = accessPackageCheckResult.Value;
                }
            }

            SystemUser newSystemUser = new()
            {
                ReporteeOrgNo = party.OrgNumber,
                SystemInternalId = regSystem.InternalId,
                IntegrationTitle = request.IntegrationTitle,
                SystemId = request.SystemId,
                PartyId = partyId
            };

            return await InsertNewSystemUser(newSystemUser, userId, regSystem, delegationCheckFinalResult, partyId, accessPackageDelegationCheckResult, partyUuid, cancellationToken);
        }

        private async Task<Result<SystemUser>> CreateSystemUserFromApprovedVendorRequest(
            SystemUserType systemUserType, 
            string systemId, 
            string partyId, 
            int userId, 
            string? externalRef, 
            Guid? requestId, 
            List<AccessPackage>? accessPackages = default, 
            List<Right>? rights = default, 
            CancellationToken cancellationToken = default)
        {
            // Step 1 in refactoring of the systemuser creation process, after this method is verified to work, 
            // refactor it with the above CreateAndDelegateSystemUser method.
            // Logic is in part moved from the RequestSystemService and in part new in L4 delivery.
            DelegationCheckResult? delegationCheckFinalResult = null;
            AccessPackageDelegationCheckResult? accessPackageDelegationCheckResult = null;

            if (requestId is null || requestId == Guid.Empty)
            {
                requestId = Guid.NewGuid();
            }

            RegisteredSystemResponse? regSystem = await _registerRepository.GetRegisteredSystemById(systemId);
            if (regSystem is null)
            {
                return Problem.SystemIdNotFound;
            }

            Party party = await _partiesClient.GetPartyAsync(int.Parse(partyId), cancellationToken);

            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.Reportee_Orgno_NotFound;
            }

            if (!party.PartyUuid.HasValue)
            {
                return Problem.Party_PartyUuid_NotFound;
            }

            Guid partyUuid = party.PartyUuid.Value;

            ExternalRequestId externalRequestId = new()
            {
                OrgNo = party.OrgNumber,
                SystemId = systemId,
                ExternalRef = string.IsNullOrEmpty(externalRef) ? party.OrgNumber : externalRef
            };

            SystemUser? existing = await _repository.GetSystemUserByExternalRequestId(externalRequestId);
            if (existing is not null)
            {
                return Problem.SystemUser_AlreadyExists;
            }

            if (rights is not null && rights.Count > 0)
            {
                Result<bool> validatedRequestedRights = ValidateRights(rights, regSystem);
                if (validatedRequestedRights.IsProblem)
                {
                    return validatedRequestedRights.Problem;
                }
            }

            if (accessPackages is not null && accessPackages.Count > 0)
            {
                Result<bool> validatedRequestedPackages = ValidateAccessPackages(accessPackages, regSystem);
                if (validatedRequestedPackages.IsProblem)
                {
                    return validatedRequestedPackages.Problem;
                }
            }

            if (systemUserType == SystemUserType.Standard && rights is not null && rights.Count > 0)
            {
                delegationCheckFinalResult = await delegationHelper.UserDelegationCheckForReportee(int.Parse(partyId), regSystem.Id, rights, fromBff:false, cancellationToken);

                if (delegationCheckFinalResult?.RightResponses is null)
                {
                    // This represents some problem with doing the delegation check beyond the rights not being delegable.
                    return Problem.UnableToDoDelegationCheck;
                }

                if (!delegationCheckFinalResult.CanDelegate)
                {
                    // This represents that the rights are not delegable, but the DelegationCheck method call has been completed.
                    return DelegationHelper.MapDetailExternalErrorListToProblemInstance(delegationCheckFinalResult.errors);
                }
            }

            if (systemUserType == SystemUserType.Standard && accessPackages is not null && accessPackages.Count > 0)
            {
                var accessPackageCheckResult = await delegationHelper.ValidateDelegationRightsForAccessPackages(partyUuid, regSystem.Id, accessPackages, fromBff:false, cancellationToken);
                if (accessPackageCheckResult.IsProblem)
                {
                    return accessPackageCheckResult.Problem;
                }
                else
                {
                    accessPackageDelegationCheckResult = accessPackageCheckResult.Value;
                }
            }

            regSystem.Name.TryGetValue("nb", out string? systemName);
            if (systemName is null)
            {
                return Problem.SystemNameNotFound;
            }

            SystemUser newSystemUser = new()
            {
                Id = requestId.ToString()!,
                ReporteeOrgNo = party.OrgNumber,
                SystemInternalId = regSystem.InternalId,
                IntegrationTitle = systemName,
                SystemId = systemId,
                PartyId = partyId,
                UserType = systemUserType,
                ExternalRef = string.IsNullOrEmpty(externalRef) ? party.OrgNumber : externalRef,
                AccessPackages = accessPackageDelegationCheckResult?.AccessPackages ?? []
            };

            if (systemUserType == SystemUserType.Agent)
            {
                newSystemUser.AccessPackages = accessPackages ?? [];
            }

            return await InsertNewSystemUser(newSystemUser, userId, regSystem, delegationCheckFinalResult, partyId, accessPackageDelegationCheckResult, partyUuid, cancellationToken);
            
        }

        private async Task<Result<SystemUser>> InsertNewSystemUser(
            SystemUser newSystemUser, 
            int userId, 
            RegisteredSystemResponse regSystem, 
            DelegationCheckResult? delegationCheckFinalResult, 
            string partyId, 
            AccessPackageDelegationCheckResult? accessPackageDelegationCheckResult, 
            Guid partyUuid, 
            CancellationToken cancellationToken)
        {
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

            if (IsStandardSystemUserDelegatgeSingleRights(newSystemUser, regSystem, delegationCheckFinalResult))
            {
                Result<bool> delegationSucceeded = await _accessManagementClient.DelegateRightToSystemUser(partyId.ToString(), inserted, delegationCheckFinalResult!.RightResponses!);
                if (delegationSucceeded.IsProblem)
                {
                    await _repository.SetDeleteSystemUserById((Guid)insertedId);
                    return delegationSucceeded.Problem;
                }
            }

            if (IsStandardSystemUserDelegateAccessPackage(newSystemUser, accessPackageDelegationCheckResult))
            {
                Result<bool> accessPackageDelegationSucceeded = await DelegateAccessPackagesToSystemUser(partyUuid, inserted, accessPackageDelegationCheckResult!.AccessPackages!, cancellationToken);
                if (accessPackageDelegationSucceeded.IsProblem)
                {
                    await _repository.SetDeleteSystemUserById((Guid)insertedId);
                    return accessPackageDelegationSucceeded.Problem;
                }
            }

            return inserted;
        }

        private static bool IsStandardSystemUserDelegateAccessPackage(SystemUser newSystemUser, AccessPackageDelegationCheckResult? accessPackageDelegationCheckResult)
        {
            if (newSystemUser.UserType == SystemUserType.Standard && accessPackageDelegationCheckResult is not null && accessPackageDelegationCheckResult.CanDelegate && accessPackageDelegationCheckResult.AccessPackages is not null && accessPackageDelegationCheckResult.AccessPackages.Count > 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsStandardSystemUserDelegatgeSingleRights(SystemUser newSystemUser, RegisteredSystemResponse regSystem, DelegationCheckResult? delegationCheckFinalResult)
        {
            if (newSystemUser.UserType == SystemUserType.Standard && regSystem.Rights is not null && regSystem.Rights.Count > 0 && delegationCheckFinalResult is not null && delegationCheckFinalResult.CanDelegate)
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public async Task<Result<SystemUser>> CreateSystemUserFromApprovedVendorRequest(
            AgentRequestSystemResponse request, 
            string partyId, 
            int userId, 
            CancellationToken cancellationToken)
        {
            return await CreateSystemUserFromApprovedVendorRequest(
                SystemUserType.Agent, 
                request.SystemId, 
                partyId, 
                userId, 
                externalRef:request.ExternalRef, 
                request.Id, 
                request.AccessPackages, 
                null, 
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Result<SystemUser>> CreateSystemUserFromApprovedVendorRequest(
            RequestSystemResponse request, 
            string partyId, 
            int userId, 
            CancellationToken cancellationToken)
        {
            return await CreateSystemUserFromApprovedVendorRequest(
                SystemUserType.Standard, 
                request.SystemId, 
                partyId, 
                userId, 
                externalRef: request.ExternalRef, 
                request.Id, 
                request.AccessPackages, 
                request.Rights, 
                cancellationToken);
        }                
     
        /// <inheritdoc/>
        public Result<bool> ValidateRights(List<Right> rights, RegisteredSystemResponse systemInfo)
        {
            if ((rights.Count > 0 && systemInfo.Rights is null) || (rights.Count > systemInfo.Rights!.Count))
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }

            bool[] validate = new bool[rights.Count];
            foreach (var requestRight in rights)
            {
                // Find the first matching Right in the list of Rights, with a matching TOP level AttributePair in the Resource list
                List<Right> topMatchesInSystem = FindListOfMatchingRightsOnTopAttribute(requestRight.Resource[0], systemInfo.Rights);
                if (topMatchesInSystem.Count == 0)
                {
                    return Problem.Rights_NotFound_Or_NotDelegable;
                }

                // Locate one full match, the first we find might not be the correct
                foreach (var systemRight in topMatchesInSystem)
                {
                    if (IsFullMatch(systemRight, requestRight))
                    {
                        validate[rights.IndexOf(requestRight)] = true;
                    }
                }
            }

            foreach (bool right in validate)
            {
                if (!right)
                {
                    return Problem.Rights_NotFound_Or_NotDelegable;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public Result<bool> ValidateAccessPackages(List<AccessPackage> accessPackages, RegisteredSystemResponse systemInfo)
        {
            if (systemInfo == null || systemInfo.AccessPackages == null)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }

            if (systemInfo.AccessPackages.Count == 0)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }

            if (accessPackages.Count > systemInfo.AccessPackages.Count)
            {
                return Problem.Rights_NotFound_Or_NotDelegable;
            }

            bool[] validate = new bool[accessPackages.Count];
            foreach (AccessPackage accessPackage in accessPackages)
            {
                foreach (AccessPackage systemPackage in systemInfo.AccessPackages)
                {
                    if (accessPackage.Urn == systemPackage.Urn)
                    {
                        validate[accessPackages.IndexOf(accessPackage)] = true;
                    }
                }
            }

            foreach (bool package in validate)
            {
                if (!package)
                {
                    return Problem.Rights_NotFound_Or_NotDelegable;
                }
            }

            return true;
        }

        // Find the first matching Right in the list of Rights, with a matching TOP level AttributePair in the Resource list
        private static List<Right> FindListOfMatchingRightsOnTopAttribute(AttributePair newpair, List<Right> systemlist)
        {
            List<Right> list = [];

            foreach (Right systemRight in systemlist)
            {
                foreach (AttributePair p in systemRight.Resource)
                {
                    if (newpair.Id == p.Id && newpair.Value == p.Value)
                    {
                        list.Add(systemRight);
                    }
                }
            }

            return list;
        }

        private static bool IsFullMatch(Right systemRight, Right requestRight)
        {
            if (requestRight.Resource.Count != systemRight.Resource.Count)
            {
                return false;
            }

            foreach (var systemPair in systemRight.Resource)
            {
                if (!VerifySubResource(systemPair, requestRight))
                {
                    return false;
                }
            }

            return true;
        }

        // Ensure that the system's sub-resource is present in the request's list of sub-resources
        private static bool VerifySubResource(AttributePair system, Right request)
        {
            foreach (var resource in request.Resource)
            {
                if (system.Id == resource.Id && system.Value == resource.Value)
                {
                    return true;
                }
            }

            return false;
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
                        AgentSystemUserId = new Guid(systemUser.Id!)
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

        /// <inheritdoc/>
        public async Task<Result<bool>> DelegateAccessPackagesToSystemUser(Guid partyUuId, SystemUser systemUser, List<AccessPackage> accessPackages, CancellationToken cancellationToken)
        {
            // Push system user to Access Management
            Result<bool> partyCreated = await _accessManagementClient.PushSystemUserToAM(partyUuId, systemUser, cancellationToken);

            if (partyCreated.IsProblem)
            {
                return partyCreated.Problem;
            }

            // Add the system user as right holder
            Result<bool> result = await _accessManagementClient.AddSystemUserAsRightHolder(partyUuId, Guid.Parse(systemUser.Id), cancellationToken);
            if (result.IsProblem)
            {
                return result.Problem;
            }

            // 2. Delegate the access packages to the system user
            foreach (AccessPackage accessPackage in accessPackages)
            {
                Result<bool> delegationResult = await _accessManagementClient.DelegateSingleAccessPackageToSystemUser(partyUuId, Guid.Parse(systemUser.Id), accessPackage.Urn!, cancellationToken);

                if (delegationResult.IsProblem)
                {
                    return new Result<bool>(delegationResult.Problem!);
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<Result<List<Customer>>> GetClientsForFacilitator(Guid facilitator, List<string> packages, IFeatureManager featureManager, CancellationToken cancellationToken)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.MockCustomerApi))
            {
                var res = await _partiesClient.GetPartyCustomers(facilitator, packages.FirstOrDefault()!, cancellationToken);
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

        /// <inheritdoc/>
        public async Task<Result<StandardSystemUserDelegations>> GetListOfDelegationsForStandardSystemUser(int partyId, Guid systemUserId, CancellationToken cancellationToken)
        {
            SystemUser? systemUser = await _repository.GetSystemUserById(systemUserId);
            if (systemUser is null)
            {
                return Problem.SystemUserNotFound;
            }

            Party party = await _partiesClient.GetPartyAsync(partyId, cancellationToken);

            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.Reportee_Orgno_NotFound;
            }

            if (!party.PartyUuid.HasValue)
            {
                return Problem.Party_PartyUuid_NotFound;
            }

            Guid partyUuId = party.PartyUuid.Value;

            string systemId = systemUser.SystemId;

            var rightsResult = await GetDelegatedRightsForSystemUser(systemUserId, partyId, cancellationToken);

            if (rightsResult.IsProblem)
            {
                return Problem.AccessPackage_FailedToGetDelegatedRights;
            }

            List<Right> rights = rightsResult.Value;
            var delegatedPackages = await GetAccessPackagesForSystemUser(partyUuId, systemUserId, cancellationToken);
            if (delegatedPackages.IsProblem)
            {
                return Problem.AccessPackage_FailedToGetDelegatedPackages;
            }

            List<AccessPackage> accessPackagesForSystemUser = delegatedPackages.Value;

            StandardSystemUserDelegations standardSystemUserDelegations = new StandardSystemUserDelegations
            {
                SystemUserId = systemUserId,
                AccessPackages = accessPackagesForSystemUser,
                Rights = rights
            };

            return standardSystemUserDelegations;
        }

        /// <summary>
        /// Gets the delegated rights for a standard system user
        /// </summary>
        /// <param name="systemUserId">the unique identifier for the system user </param>
        /// <param name="partyId">the unique identifier for the party</param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns>lsit of delegated rights for the system user</returns>
        public async Task<Result<List<Right>>> GetDelegatedRightsForSystemUser(Guid systemUserId, int partyId, CancellationToken cancellationToken)
        {
            var rightsDelegationResult = await _accessManagementClient.GetSingleRightDelegationsForStandardUser(systemUserId, partyId, cancellationToken);
            if (rightsDelegationResult.IsProblem)
            {
                return rightsDelegationResult.Problem!;
            }

            List<RightDelegation> rightDelegations = rightsDelegationResult.Value;
            
            // Map RightDelegation to Right model
            var rights = rightDelegations.Select(rd => new Right
            {
                Resource = rd.Resource.Select(attr => new AttributePair
                {
                    Id = attr.Id,
                    Value = attr.Value
                }).ToList(),
            }).ToList();

            return rights;
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
                    AssignmentId = item.Delegation.ToId,
                    CustomerName = item.From.Name
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

        private async Task<Result<Guid>> GetPartyUuId(int partyId, CancellationToken cancellationToken)
        {
            Party party = await _partiesClient.GetPartyAsync(partyId, cancellationToken);

            if (party is null || string.IsNullOrEmpty(party.OrgNumber))
            {
                return Problem.Reportee_Orgno_NotFound;
            }

            if (!party.PartyUuid.HasValue)
            {
                return Problem.Party_PartyUuid_NotFound;
            }

            Guid partyUuid = party.PartyUuid.Value;
            return partyUuid;
        }

        private async Task<Result<List<AccessPackage>>> GetAccessPackagesForSystemUser(Guid partyUuId, Guid systemUserId, CancellationToken cancellationToken)
        {
            List<PackagePermission> packagePermissions = [];

            await foreach (var result in _accessManagementClient.GetAccessPackagesForSystemUser(partyUuId, systemUserId, cancellationToken))
            {
                if (result.IsProblem)
                {
                    return result.Problem;
                }

                if (result.IsSuccess && result.Value is not null)
                {
                    packagePermissions.Add(result.Value);
                }
            }

            // 3. Process results
            GetDelegatedPackagesFromDelegations(packagePermissions, out List<AccessPackage> accessPackages);
            return accessPackages;
        }

        private static void GetDelegatedPackagesFromDelegations(
            List<PackagePermission> delegations,
            out List<AccessPackage> accessPackages)
        {
            accessPackages = [];
            foreach (PackagePermission packagePermission in delegations)
            {
                if (packagePermission.Package is not null)
                {
                    AccessPackage accessPackage = new AccessPackage();
                    accessPackage.Urn = packagePermission.Package.Urn;
                    accessPackages.Add(accessPackage);
                }
            }
        }        
    }
}
