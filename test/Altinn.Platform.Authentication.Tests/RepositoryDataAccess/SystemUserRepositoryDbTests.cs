using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

public class SystemUserRepositoryDbTests : DbTestBase
{
    protected ISystemUserRepository Repository => Services.GetRequiredService<ISystemUserRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
        base.ConfigureServices(services);
    }

    [Fact]
    public async Task InsertSystemUser()
    {
        await Repository.InsertSystemUser(new Core.Models.SystemUser 
        {
            Description = "TestDescription",
            IntegrationTitle = "TestIntegrationTitle",
            OwnedByPartyId = "1",
            ProductName = "Awesome_System",
            SupplierName = "Awesome Supplier AS",
            SupplierOrgNo = "123456789 MVA"
        });
    }

    //[Fact]
    //public async Task GetAllActiveSystemUsersForParty()
    //{

    //}

    //[Fact]
    //public async Task GetSystemUserById()
    //{

    //}

    //[Fact]
    //public async Task SetDeleteSystemUserById()
    //{

    //}
}