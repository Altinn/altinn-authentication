using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

#nullable enable
public class SystemRegisterRepositoryDbTests(DbFixture dbFixture) 
    : DbTestBase(dbFixture)
{
    protected ISystemRegisterRepository Repository => Services.GetRequiredService<ISystemRegisterRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureHost(IHostApplicationBuilder builder)
    {
        builder.AddPersistanceLayer();
        base.ConfigureHost(builder);
    }
}