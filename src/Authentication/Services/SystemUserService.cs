using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// The service that supports the SystemUser CRUD APIcontroller
    /// </summary>
    public class SystemUserService : ISystemUserService
    {
        private readonly List<SystemUser> theMockList;

        /// <summary>
        /// The Constructor
        /// </summary>
        public SystemUserService()
        {
            theMockList = MockDataHelper();
        }

        /// <summary>
        /// Creates a new SystemUser
        /// The unique Id for the systemuser is handled by the db.
        /// But the calling client may send a guid for the request of creating a new system user
        /// to ensure that there is no mismatch if the same partyId creates several new SystemUsers at the same time
        /// </summary>
        /// <returns></returns>
        public Task<SystemUser> CreateSystemUser(SystemUser request)
        {
            if (int.Parse(request.OwnedByPartyId) < 1)
            {
                return Task.FromResult<SystemUser>(null);
            }

            theMockList.Add(request);
            return Task.FromResult(request);
        }

        /// <summary>
        /// Returns the list of SystemUsers this PartyID has registered, including "deleted" ones.
        /// </summary>
        /// <returns></returns>
        public Task<List<SystemUser>> GetListOfSystemUsersPartyHas(int partyId)
        {
            if (partyId < 1)
            {
                return Task.FromResult<List<SystemUser>>(null);
            }

            return Task.FromResult(theMockList);
        }

        /// <summary>
        /// Return a single SystemUser by PartyId and SystemUserId
        /// </summary>
        /// <returns></returns>
        public Task<SystemUser> GetSingleSystemUserById(Guid systemUserId)
        {
            SystemUser search = theMockList.Find(s => s.Id == systemUserId.ToString());
            
            return Task.FromResult(search);
        }

        /// <summary>
        /// Set the Delete flag on the identified SystemUser
        /// </summary>
        /// <returns></returns>
        public Task<int> SetDeleteFlagOnSystemUser(Guid systemUserId)
        {
            SystemUser toBeDeleted = theMockList.Find(s => s.Id == systemUserId.ToString());
            toBeDeleted.IsDeleted = true;
            return Task.FromResult(1);
        }

        /// <summary>
        /// Replaces the values for the existing system user with those from the update 
        /// </summary>
        /// <returns></returns>
        public Task<int> UpdateSystemUserById(Guid systemUserId, SystemUser request)
        {
            int array = theMockList.FindIndex(su => su.Id == request.Id);
            theMockList[array] = request;
            return Task.FromResult(1);
        }

        /// <summary>
        /// Helper method during development, just some Mock data.
        /// </summary>
        /// <returns></returns>
        private static List<SystemUser> MockDataHelper()
        {            
            SystemUser systemUser1 = new()
            {
                Id = "37ce1792-3b35-4d50-a07d-636017aa7dbd",
                IntegrationTitle = "Vårt regnskapsystem",
                Description = "Koblet opp mot Visma. Snakk med Pål om abonnement",
                ProductName = "visma_vis_v2",
                OwnedByPartyId = "orgno:91235123",
                Created = "2023-09-12",
                IsDeleted = false,
                ClientId = string.Empty
            };

            SystemUser systemUser2 = new()
            {
                Id = "37ce1792-3b35-4d50-a07d-636017aa7dbe",
                IntegrationTitle = "Vårt andre regnskapsystem",
                Description = "Snakk med Per om abonnement",
                ProductName = "visma_vis_sys",
                OwnedByPartyId = "orgno:91235124",
                Created = "2023-09-22",
                IsDeleted = false,
                ClientId = string.Empty
            };

            SystemUser systemUser3 = new()
            {
                Id = "37ce1792-3b35-4d50-a07d-636017aa7dbf",
                IntegrationTitle = "Et helt annet system",
                Description = "Kai og Guri vet alt om dette systemet.",
                ProductName = "fiken_superskatt",
                OwnedByPartyId = "orgno:91235125",
                Created = "2023-09-22",
                IsDeleted = false,
                ClientId = string.Empty
            };

            List<SystemUser> systemUserList = new()
        {
            systemUser1,
            systemUser2,
            systemUser3
        };
            return systemUserList;
        }
    }
}
