using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Persistance.Configuration;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;
using Yuniql.Core;

#nullable enable
namespace Altinn.Platform.Persistence.Tests;

public abstract class DbTestBase : 
    IAsyncLifetime,
    IClassFixture<DbFixture>
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("docker.io/postgres:15.4-alpine")
        .WithDatabase("authentication")
        .WithUsername("platform_authentication")
        .WithPassword("Postgres")
        .WithCleanUp(true)
        .Build();

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

        await _dbContainer.DisposeAsync();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var connectionString = _dbContainer.GetConnectionString();

        var container = new ServiceCollection();
        container.AddLogging(l => l.AddConsole());
        container.Configure<PostgreSqlSettings>(settings =>
        {
            settings.ConnectionString = connectionString;
            settings.AuthenticationDbPassword = "unused";
        });
        ConfigureServices(container);

        var configuration = new Yuniql.AspNetCore.Configuration();
        configuration.Platform = SUPPORTED_DATABASES.POSTGRESQL;
        configuration.Workspace = Path.Combine(FindWorkspace(), "src", "Persistance", "Migration");
        configuration.ConnectionString = connectionString;
        configuration.IsAutoCreateDatabase = false;

        var traceService = TraceService.Instance;
        var dataService = new Yuniql.PostgreSql.PostgreSqlDataService(traceService);
        var bulkImportService = new Yuniql.PostgreSql.PostgreSqlBulkImportService(traceService);
        var migrationServiceFactory = new MigrationServiceFactory(traceService);
        var migrationService = migrationServiceFactory.Create(dataService, bulkImportService);
        ConfigurationHelper.Initialize(configuration);
        migrationService.Run();

        _services = container.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        _scope = _services.CreateAsyncScope();

        static string FindWorkspace()
        {
            var dir = Environment.CurrentDirectory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException("Workspace directory not found");
        }
    }

    protected class TraceService : Yuniql.Extensibility.ITraceService
    {
        public static Yuniql.Extensibility.ITraceService Instance { get; } = new TraceService();

        /// <inheritdoc/>
        public bool IsDebugEnabled { get; set; } = false;

        /// <inheritdoc/>
        public bool IsTraceSensitiveData { get; set; } = false;

        /// <inheritdoc/>
        public bool IsTraceToFile { get; set; } = false;

        /// <inheritdoc/>
        public bool IsTraceToDirectory { get; set; } = false;

        /// <inheritdoc/>        
        public string? TraceDirectory { get; set; }

        /// <inheritdoc/>
        public void Info(string message, object? payload = null)
        {
            var traceMessage = $"INF   {DateTime.UtcNow.ToString("o")}   {message}{Environment.NewLine}";
            Console.Write(traceMessage);
        }

        /// <inheritdoc/>
        public void Error(string message, object? payload = null)
        {
            var traceMessage = $"ERR   {DateTime.UtcNow.ToString("o")}   {message}{Environment.NewLine}";
            Console.Write(traceMessage);
        }

        /// <inheritdoc/>
        public void Debug(string message, object? payload = null)
        {
            if (IsDebugEnabled)
            {
                var traceMessage = $"DBG   {DateTime.UtcNow.ToString("o")}   {message}{Environment.NewLine}";
                Console.Write(traceMessage);
            }
        }

        /// <inheritdoc/>
        public void Success(string message, object? payload = null)
        {
            var traceMessage = $"INF   {DateTime.UtcNow.ToString("u")}   {message}{Environment.NewLine}";
            Console.Write(traceMessage);
        }

        /// <inheritdoc/>
        public void Warn(string message, object? payload = null)
        {
            var traceMessage = $"WRN   {DateTime.UtcNow.ToString("o")}   {message}{Environment.NewLine}";
            Console.Write(traceMessage);
        }
    }
}
