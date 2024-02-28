using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

#nullable enable
public class SystemRegisterRepositoryDbTests : DbTestBase
{
    protected ISystemRegisterRepository Repository => Services.GetRequiredService<ISystemRegisterRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
        base.ConfigureServices(services);
    }

    [Fact]
    public async Task SystemRegister_InsertRegisteredSystem()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisteredSystem
            {
                Description = "Test",
                SystemTypeId = friendlyId,
                SystemVendor = "Awesome"
            });

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        RegisteredSystem? isitthere = await Repository.GetRegisteredSystemById(friendlyId);
        Assert.True(isitthere is not null);
        Assert.Equal("Awesome", isitthere.SystemVendor);
    }        
}