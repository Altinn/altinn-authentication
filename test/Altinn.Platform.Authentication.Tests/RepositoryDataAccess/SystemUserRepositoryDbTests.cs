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
public class SystemUserRepositoryDbTests : DbTestBase
{
    protected ISystemUserRepository Repository => Services.GetRequiredService<ISystemUserRepository>();

    protected ISystemRegisterRepository RegisterRepository => Services.GetRequiredService<ISystemRegisterRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
        base.ConfigureServices(services);
    }

    [Fact]
    public async Task InsertSystemUser()
    {
        Guid guid = Guid.NewGuid();

        RegisteredSystem registeredSystem = new() { SystemTypeId = "Awesome_System" };

        await RegisterRepository.CreateClient(guid.ToString());
        await RegisterRepository.CreateRegisteredSystem(registeredSystem);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser 
        {
            IntegrationTitle = "InsertSystemUserTitle",
            OwnedByPartyId = "1",
            ProductName = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA",
            ClientId = guid
        });

        Assert.True(systemUserId is not null);
    }

    [Fact]
    public async Task GetAllActiveSystemUsersForParty()
    {
        Guid guid = Guid.NewGuid();

        RegisteredSystem registeredSystem = new() { SystemTypeId = "Awesome_System" };

        await RegisterRepository.CreateClient(guid.ToString());
        await RegisterRepository.CreateRegisteredSystem(registeredSystem);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            IntegrationTitle = "GetAllActiveSystemUsersForPartyTitle",
            OwnedByPartyId = "1",
            ProductName = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA",
            ClientId = guid
        });

        var res = await Repository.GetAllActiveSystemUsersForParty(1);

        Assert.True(res is not null && res.Count > 0 && res.Find((SystemUser usr) => usr.Id == systemUserId.ToString()) is not null);
    }

    [Fact]
    public async Task GetSystemUserById()
    {
        Guid guid = Guid.NewGuid();

        RegisteredSystem registeredSystem = new() { SystemTypeId = "Awesome_System" };

        await RegisterRepository.CreateClient(guid.ToString());
        await RegisterRepository.CreateRegisteredSystem(registeredSystem);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            IntegrationTitle = "GetSystemUserByIdTitle",
            OwnedByPartyId = "1",
            ProductName = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA",
            ClientId = guid
        });

        SystemUser? systemUser = await Repository.GetSystemUserById((Guid)systemUserId);

        Assert.True(systemUser is not null && systemUser.Id == systemUserId.ToString());
    }

    [Fact]
    public async Task SetDeleteSystemUserById()
    {
        Guid guid = Guid.NewGuid();

        RegisteredSystem registeredSystem = new() { SystemTypeId = "Awesome_System" };

        await RegisterRepository.CreateClient(guid.ToString());
        await RegisterRepository.CreateRegisteredSystem(registeredSystem);

        Guid? systemUserId = await Repository.InsertSystemUser(new Core.Models.SystemUser
        {
            IntegrationTitle = "GetSystemUserByIdTitle",
            OwnedByPartyId = "1",
            ProductName = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA",
            ClientId = guid
        });

        await Repository.SetDeleteSystemUserById((Guid)systemUserId);

        var res = await Repository.GetAllActiveSystemUsersForParty(1);

        Assert.True(res.Find((SystemUser usr) => usr.Id == systemUserId.ToString()) is null);
    }
}