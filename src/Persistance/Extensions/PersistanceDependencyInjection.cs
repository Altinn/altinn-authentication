using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.Extensions;

/// <summary>
/// Extension class used for injecting the repository into the Service Collection of the parent project.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PersistanceDependencyInjection
{
    /// <summary>
    /// Used in the parent project to insert the Repositories and Postgres DB into the Dependency Injection Service Collection
    /// </summary>
    /// <param name="builder">Host application builder</param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddPersistanceLayer(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        if (services.Contains(Marker.Descriptor))
        {
            return builder;
        }

        services.Add(Marker.Descriptor);
        AddPostgreSqlDatabase(builder);
        AddSystemUserRepository(services);        
        AddSystemRegisterRepository(services);
        AddRequestRepository(services);
        AddChangeRequestRepository(services);
        AddSystemChangeLogRepository(services);
        AddOidcServerClientRepository(services);
        AddLoginTransactionRepository(services);
        AddUpstreamLoginTransactionRepository(services);
        AddOidcSessionRepository(services);
        AddAuthorizationCodeRepository(services);
        AddRefreshTokenRepository(services);
        AddUnregisteredClientRequestRepository(services);
        return builder;
    }

    private static void AddRequestRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IRequestRepository, RequestRepository>();
    }

    private static void AddChangeRequestRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IChangeRequestRepository, ChangeRequestRepository>();
    }

    /// <summary>
    /// Helper method for DI
    /// </summary>
    /// <param name="builder">Host application builder</param>
    private static void AddPostgreSqlDatabase(this IHostApplicationBuilder builder)
    {
        var fs = new ManifestEmbeddedFileProvider(typeof(PersistanceDependencyInjection).Assembly, "Migration");

        builder.AddAltinnPostgresDataSource()
            .EnableDynamicJson()
            .MapEnum<SystemUserType>("business_application.systemuser_type")
            .MapEnum<SystemChangeType>("business_application.systemchange_type")
            .AddYuniqlMigrations(typeof(PersistanceDependencyInjection), cfg =>
            {
                cfg.WorkspaceFileProvider = fs;
                cfg.Workspace = "/";
            });

        builder.Services.TryAddTransient((IServiceProvider sp) => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection());
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddSystemUserRepository(this IServiceCollection services) 
    {
        services.TryAddTransient<ISystemUserRepository, SystemUserRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddSystemRegisterRepository(this IServiceCollection services)
    {
        services.TryAddTransient<ISystemRegisterRepository, SystemRegisterRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddOidcServerClientRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IOidcServerClientRepository, OidcServerClientRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddRefreshTokenRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IRefreshTokenRepository, RefreshTokenRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddAuthorizationCodeRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IAuthorizationCodeRepository, AuthorizationCodeRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddOidcSessionRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IOidcSessionRepository, OidcSessionRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddLoginTransactionRepository(this IServiceCollection services)
    {
        services.TryAddTransient<ILoginTransactionRepository, LoginTransactionRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddUnregisteredClientRequestRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IUnregisteredClientRepository, UnregisteredClientRequestRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddUpstreamLoginTransactionRepository(this IServiceCollection services)
    {
        services.TryAddTransient<IUpstreamLoginTransactionRepository, UpstreamLoginTransactionRepository>();
    }

    /// <summary>
    /// Extension method for DI
    /// </summary>
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddSystemChangeLogRepository(this IServiceCollection services)
    {
        services.TryAddTransient<ISystemChangeLogRepository, SystemChangeLogRepository>();
    }

    private sealed record Marker
    {
        public static ServiceDescriptor Descriptor { get; } = ServiceDescriptor.Singleton<Marker, Marker>();
    }
}
