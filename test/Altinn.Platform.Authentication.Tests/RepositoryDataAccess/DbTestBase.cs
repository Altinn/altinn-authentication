using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private ServiceProvider? _services;
    private AsyncServiceScope _scope;

    protected IServiceProvider Services => _scope!.ServiceProvider;

    protected virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_scope is { } scope)
        {
            await scope.DisposeAsync();
        }

        if (_services is { } services)
        {
            await services.DisposeAsync();
        }

        if (_db is { } db)
        {
            await db.DisposeAsync();
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var container = new ServiceCollection();
        container.AddLogging(l => l.AddConsole());
        _db = await _dbFixture.CreateDbAsync();
        _db.ConfigureServices(container);
        ConfigureServices(container);

        _services = container.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        _scope = _services.CreateAsyncScope();
    }
}