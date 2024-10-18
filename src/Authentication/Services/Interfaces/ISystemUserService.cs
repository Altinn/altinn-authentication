using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;

namespace Altinn.Platform.Authentication.Services.Interfaces;
#nullable enable
/// <summary>
/// The service that supports the System User CRUD APIcontroller
/// </summary>
public interface ISystemUserService
{
    /// <summary>
    /// Returns the list of SystemUsers this PartyID has registered
    /// </summary>
    /// <param name="partyId">The User id for the Legal Entity (Organisation or Person) the Caller represent.</param> 
    /// <returns></returns>
    Task<List<SystemUser>> GetListOfSystemUsersForParty(int partyId);

    /// <summary>
    /// Return a single SystemUser by PartyId and SystemUserId
    /// </summary>
    /// <param name="systemUserId">The db id for the SystemUser to be retrieved</param>
    /// <returns></returns>
    Task<SystemUser> GetSingleSystemUserById(Guid systemUserId);

    /// <summary>
    /// Set the Delete flag on the identified SystemUser
    /// </summary>
    /// <param name="systemUserId">The db id for the SystemUser to be deteled</param>
    /// <returns></returns>
    Task<bool> SetDeleteFlagOnSystemUser(Guid systemUserId);

    /// <summary>
    /// Creates a new SystemUser
    /// The unique Id for the systemuser is handled by the db.
    /// But the calling client may send a guid for the request of creating a new system user
    /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
    /// </summary>
    /// <param name="party">The partyId for the reportee</param>
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param>
    /// <param name="userId">The userid for the loged in Reporter User.</param>
    /// <returns></returns> 
    Task<SystemUser?> CreateSystemUser(string party, SystemUserRequestDto request, int userId);

    /// <summary>
    /// Replaces the values for the existing system user with those from the update 
    /// </summary>
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param> 
    /// <returns>Returns the number of rows affected</returns>
    Task<int> UpdateSystemUserById(SystemUserUpdateDto request);

    /// <summary>
    /// Used by IdPorten, to find if a given systemOrg owns a SystemUser Integration for a Vendor's Product, by ClientId.
    /// ConsumerId is the first entry in the path.
    /// SystemOrg is the second entry in the path.
    /// ClientId is the third entry in the path.
    /// </summary>
    /// <param name="clientId">The unique id maintained by IdPorten tying their clients to the Registered Systems we maintain</param>        
    /// <param name="systemProviderOrgNo">The legal number (Orgno) of the Vendor creating the Registered System (Accounting system)</param>
    /// <param name="systemUserOwnerOrgNo">The legal number (Orgno) of the party owning the System User Integration</param>
    /// <param name="cancellationToken">Cancellationtoken</param>/// 
    /// <returns>The SystemUserIntegration model API DTO</returns>
    Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of SystemUsers the Vendor has for a given system they own.
    /// </summary>
    /// <param name="vendorOrgNo">The logged in vendor</param>
    /// <param name="systemId">The system the Vendor wants the list for</param>
    /// <param name="continueRequest">The Guid denoting from where to continue with Pagination</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Status response model CreateRequestSystemUserResponse</returns>
    Task<Result<Page<SystemUser, string>>> GetAllSystemUsersByVendorSystem(OrganisationNumber vendorOrgNo, string systemId, Page<string>.Request continueRequest, CancellationToken cancellationToken);

    /// <summary>
    /// Called from the Authn.UI BFF to create a new SystemUser and delegate it to the given Reportee Party,
    /// returns either the new SystemUser or the ProblemInstance from the Result
    /// </summary>
    /// <param name="party">the Reportee partyId</param>
    /// <param name="request">the requested SystemUser to be created</param>
    /// <param name="userId">the logged in user</param>
    /// <returns>The CreateSystemUserResponse response model, with either a new SystemUser model inside, or a list of errors.</returns>
    Task<Result<CreateSystemUserResponse>> CreateAndDelegateSystemUser(string party, SystemUserRequestDto request, int userId, CancellationToken cancellationToken);
}
