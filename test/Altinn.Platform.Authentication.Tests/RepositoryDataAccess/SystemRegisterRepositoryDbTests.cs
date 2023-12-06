using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

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
    public async Task CreatePartyRegistry()
    {
        // var info1 = await Repository.
    }
}