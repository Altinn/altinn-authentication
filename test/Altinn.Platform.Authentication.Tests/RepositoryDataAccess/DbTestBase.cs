using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

#nullable enable
namespace Altinn.Platform.Persistence.Tests;

public abstract class DbTestBase
    : IAsyncLifetime,
    IClassFixture<DbFixture>
{
    private readonly DbFixture _dbFixture;
    private DbFixture.OwnedDb? _db;

    protected DbTestBase(DbFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    private IHost? _host;
    private AsyncServiceScope _scope;

    protected IServiceProvider Services => _scope!.ServiceProvider;

    protected virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected virtual void ConfigureHost(IHostApplicationBuilder builder)
    {
        builder.AddPersistanceLayer();
        ConfigureServices(builder.Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_scope is { } scope)
        {
            await scope.DisposeAsync();
        }

        if (_host is { } host)
        {
            await host.StopAsync();
            host.Dispose();
        }

        if (_db is { } db)
        {
            await db.DisposeAsync();
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var configuration = new ConfigurationManager();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "test",
            EnvironmentName = "Development",
            Configuration = configuration,
        });

        builder.AddAltinnServiceDefaults("authentication");

        _db = await _dbFixture.CreateDbAsync();
        _db.ConfigureApplication(builder);

        ConfigureHost(builder);

        _host = builder.Build();
        await _host.StartAsync();

        _scope = _host.Services.CreateAsyncScope();
    }
}