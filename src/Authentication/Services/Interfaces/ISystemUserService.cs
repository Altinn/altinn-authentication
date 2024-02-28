using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;

namespace Altinn.Platform.Authentication.Services.Interfaces;

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
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param> 
    /// <param name="partyId">The user id for the Legal Entity (Organisation or Person) the Caller represent.</param> 
    /// <returns></returns> 
    Task<SystemUser> CreateSystemUser(SystemUserRequestDto request, int partyId);

    /// <summary>
    /// Replaces the values for the existing system user with those from the update 
    /// </summary>
    /// <param name="request">The DTO describing the Product the Caller wants to create.</param> 
    /// <returns>Returns the number of rows affected</returns>
    Task<int> UpdateSystemUserById(SystemUserUpdateDto request);
}
