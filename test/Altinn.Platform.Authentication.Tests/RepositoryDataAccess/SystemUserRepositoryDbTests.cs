using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

#nullable enable
public class SystemUserRepositoryDbTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
{
    protected ISystemUserRepository Repository => Services.GetRequiredService<ISystemUserRepository>();

    protected ISystemRegisterRepository RegisterRepository => Services.GetRequiredService<ISystemRegisterRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
        base.ConfigureServices(services);
    }

    /// <summary>
    /// Inserts a new SystemUser, using the same input expected from GUI or API
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task InsertSystemUser()
    {
        Guid guid = Guid.NewGuid();
        string[] defaultRights = [];
        List<string> clientId = new List<string>();
        clientId.Add(guid.ToString());

        RegisterSystemRequest registeredSystem = new() { SystemId = "Awesome_System", SystemVendorOrgNumber = "991825827", SystemName = "Awesome System", ClientId = clientId, SoftDeleted = false };

        await RegisterRepository.CreateClient(guid.ToString());
        Guid? createdSystemInternalId = await RegisterRepository.CreateRegisteredSystem(registeredSystem, defaultRights);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser 
        {
            SystemInternalId = createdSystemInternalId,
            IntegrationTitle = "InsertSystemUserTitle",
            PartyId = "1",
            SystemId = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA"
        });

        Assert.True(systemUserId is not null);
    }

    /// <summary>
    /// Used to populate the Overview page for a specific user
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetAllActiveSystemUsersForParty()
    {
        Guid guid = Guid.NewGuid();
        string[] defaultRights = [];
        List<string> clientId = new List<string>();
        clientId.Add(guid.ToString());
        List<Right> rights = new List<Right>()
                {
                    new Right()
                    {
                        Resource = new List<AttributePair>()
                        {
                            new AttributePair()
                            {
                                Id = "urn:altinn:resource",
                                Value = "Test"
                            }
                        }
                    }
                };

        RegisterSystemRequest registeredSystem = new() { SystemId = "Awesome_System", SystemVendorOrgNumber="991825827", SystemName="Awesome System", ClientId = clientId, SoftDeleted = false };

        await RegisterRepository.CreateClient(guid.ToString());
        Guid? createdSystemInternalId = await RegisterRepository.CreateRegisteredSystem(registeredSystem, defaultRights);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            SystemInternalId = createdSystemInternalId,
            IntegrationTitle = "GetAllActiveSystemUsersForPartyTitle",
            PartyId = "1",
            SystemId = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA"
        });

        var res = await Repository.GetAllActiveSystemUsersForParty(1);

        Assert.True(res is not null && res.Count > 0 && res.Find((SystemUser usr) => usr.Id == systemUserId.ToString()) is not null);
    }

    /// <summary>
    /// Retrieves a specific SystemUserIntegration
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSystemUserById()
    {
        Guid guid = Guid.NewGuid();
        string[] defaultRights = [];

        List<string> clientId = [guid.ToString()];

        RegisterSystemRequest registeredSystem = new() { SystemId = "Awesome_System", SystemVendorOrgNumber = "991825827", SystemName = "Awesome System", ClientId = clientId, SoftDeleted = false };

        await RegisterRepository.CreateClient(guid.ToString());
        Guid? createdSystemInternalId = await RegisterRepository.CreateRegisteredSystem(registeredSystem, defaultRights);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            SystemInternalId = createdSystemInternalId,
            IntegrationTitle = "GetSystemUserByIdTitle",
            PartyId = "1",
            SystemId = "Awesome_System",            
            SupplierOrgNo = "1234567890"
        });

        SystemUser? systemUser = await Repository.GetSystemUserById((Guid)systemUserId);

        Assert.True(systemUser is not null && systemUser.Id == systemUserId.ToString());
    }

    /// <summary>
    /// Sets the SystemUserIntegration to be in a "deleted" state.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task SoftDeleteSystemUserById()
    {
        Guid guid = Guid.NewGuid();
        string[] defaultRights = [];

        List<string> clientId = new List<string>();
        clientId.Add(guid.ToString());

        RegisterSystemRequest registeredSystem = new() { SystemId = "Awesome_System", SystemVendorOrgNumber = "991825827", SystemName = "Awesome System", ClientId = clientId, SoftDeleted = false };

        await RegisterRepository.CreateClient(guid.ToString());
        Guid? createdSystemInternalId = await RegisterRepository.CreateRegisteredSystem(registeredSystem, defaultRights);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            SystemInternalId = createdSystemInternalId,
            IntegrationTitle = "GetSystemUserByIdTitle",
            PartyId = "1",
            SystemId = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA"
        });

        await Repository.SetDeleteSystemUserById((Guid)systemUserId);

        var res = await Repository.GetAllActiveSystemUsersForParty(1);

        Assert.True(res.Find((SystemUser usr) => usr.Id == systemUserId.ToString()) is null);
    }
}