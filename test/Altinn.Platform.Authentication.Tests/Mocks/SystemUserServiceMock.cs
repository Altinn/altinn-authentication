using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Parties;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Azure.Core;

namespace Altinn.Platform.Authentication.Tests.Mocks;

/// <summary>
/// The service that supports the SystemUser CRUD APIcontroller
/// </summary>
[ExcludeFromCodeCoverage]
public class SystemUserServiceMock : ISystemUserService
{
    private readonly List<SystemUser> theMockList;

    /// <summary>
    /// The Constructor
    /// </summary>
    public SystemUserServiceMock()
    {
        theMockList = MockDataHelper();
    }

    /// <summary>
    /// Returns the list of SystemUsers this PartyID has registered, including "deleted" ones.
    /// </summary>
    /// <returns></returns>
    public Task<List<SystemUser>> GetListOfSystemUsersForParty(int partyId)
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
    /// Replaces the values for the existing system user with those from the update 
    /// </summary>
    /// <returns></returns>
    public Task<int> UpdateSystemUserById(SystemUserUpdateDto request)
    {
        int array = theMockList.FindIndex(su => su.Id == request.Id.ToString());
        theMockList[array].IntegrationTitle = request.IntegrationTitle;
        theMockList[array].SystemId = request.SystemId;
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
            SystemId = "supplier_name_cool_system",
            PartyId = "orgno:91235123",
            IsDeleted = false,
            SupplierName = "Supplier1 Name",
            SupplierOrgNo = "123456789"
        };

        SystemUser systemUser2 = new()
        {
            Id = "37ce1792-3b35-4d50-a07d-636017aa7dbe",
            IntegrationTitle = "Vårt andre regnskapsystem",
            SystemId = "supplier2_product_name",
            PartyId = "orgno:91235124",
            IsDeleted = false,
            SupplierName = "Supplier2 Name",
            SupplierOrgNo = "123456789"
        };

        SystemUser systemUser3 = new()
        {
            Id = "37ce1792-3b35-4d50-a07d-636017aa7dbf",
            IntegrationTitle = "Et helt annet system",
            SystemId = "supplier3_product_name",
            PartyId = "orgno:91235125",
            IsDeleted = false,
            SupplierName = "Supplier3 Name",
            SupplierOrgNo = "123456789"
        };

        List<SystemUser> systemUserList = new()
    {
        systemUser1,
        systemUser2,
        systemUser3
    };
        return systemUserList;
    }

    public Task<SystemUser> CheckIfPartyHasIntegration(string clientId, string consumerId, string systemOrg, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SystemUser
        {
            Id = "37ce1792-3b35-4d50-a07d-636017aa7dbf",
            IntegrationTitle = "Et helt annet system",
            SystemId = "supplier3_product_name",
            PartyId = "orgno:" + systemOrg,
            IsDeleted = false,
            SupplierName = "Supplier3 Name",
            SupplierOrgNo = consumerId,
            ExternalRef = systemOrg
        });
    }

    public Task<SystemUser> CreateSystemUser(string party, SystemUserRequestDto request, int userId)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<Result<Page<SystemUser, string>>> GetAllSystemUsersByVendorSystem(OrganisationNumber vendorOrgNo, string systemId, Page<string>.Request cont, CancellationToken cancellationToken)
    {
        List<SystemUser> theList = [];

        theList.Add(new SystemUser
        {
            Id = "37ce1792-3b35-4d50-a07d-636017aa7dbf",
            IntegrationTitle = "Et helt annet system",
            SystemId = systemId,
            PartyId = "orgno:123456789",
            IsDeleted = false,
            SupplierName = "Supplier3 Name",
            SupplierOrgNo = vendorOrgNo.ID
        });

        return Page.Create<SystemUser, string>(theList, 2, static theList => theList.Id);
    }

    /// <summary>
    /// not in use, the Test Explorer uses the real SystemUserService in TestContainers
    /// </summary>     
    /// <returns></returns>    
    public Task<Result<SystemUser>> CreateAndDelegateSystemUser(string party, SystemUserRequestDto request, int userId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Set the Delete flag on the identified SystemUser
    /// </summary>
    /// <returns></returns>
    public Task<bool> SetDeleteFlagOnSystemUser(string partyId, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        SystemUser toBeDeleted = theMockList.Find(s => s.Id == systemUserId.ToString());
        toBeDeleted.IsDeleted = true;
        return Task.FromResult(true);
    }

    public Task<SystemUser> GetSystemUserByExternalRequestId(ExternalRequestId externalRequestId)
    {
        throw new NotImplementedException();
    }
}
