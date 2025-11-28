#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Altinn.Platform.Authentication.Tests;

public abstract class WebApplicationTests
    : IClassFixture<DbFixture>,
    IClassFixture<WebApplicationFixture>,
    IAsyncLifetime
{
    private readonly DbFixture _dbFixture;
    private readonly WebApplicationFixture _webApplicationFixture;

    public WebApplicationTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
    {
        _dbFixture = dbFixture;
        _webApplicationFixture = webApplicationFixture;
    }

    private WebApplicationFactory<Program> _webApp;
    private IServiceProvider _services;
    private AsyncServiceScope _scope;
    private DbFixture.OwnedDb? _db;

    protected IServiceProvider Services => _scope!.ServiceProvider;

    protected TimeProvider TimeProvider => Services.GetRequiredService<TimeProvider>();

    protected HttpClient CreateClient()
        => _webApp!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost"), });

    protected virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProfile, ProfileMock>();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_scope is { } scope)
        {
            await scope.DisposeAsync();
        }

        if (_services is IAsyncDisposable iad)
        {
            await iad.DisposeAsync();
        }
        else if (_services is IDisposable id)
        {
            id.Dispose();
        }

        if (_webApp is { } webApp)
        {
            await webApp.DisposeAsync();
        }

        if (_db is { } db)
        {
            await db.DisposeAsync();
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        _db = await _dbFixture.CreateDbAsync();
        _webApp = _webApplicationFixture.CreateServer(builder =>
        {
            var settings = new ConfigurationBuilder();

            settings.AddInMemoryCollection([
                new("Altinn:Npgsql:authentication:Enable", "true"),
                new("Altinn:Npgsql:authentication:Migrate:Enabled", "true"),

                new("Altinn:Npgsql:authentication:ConnectionString", _db.ConnectionString),
                new("Altinn:Npgsql:authentication:Migrate:ConnectionString", _db.ConnectionString),
            ]);

            builder.UseConfiguration(settings.Build());
            builder.ConfigureServices(services =>
            {
                ConfigureServices(services);
            });
        });

        // Force the server to start - runs migrations etc
        _webApp.CreateClient();

        _services = _webApp.Services;
        _scope = _services.CreateAsyncScope();
    }
}
