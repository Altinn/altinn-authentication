using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Configuration;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    /// <param name="services">Parent project's Service Collection</param>
    /// <returns></returns>
    public static IServiceCollection AddPersistanceLayer(this IServiceCollection services)
    {
        AddPostgreSqlDatabase(services);
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
        return services;
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
    /// <param name="services">IServiceCollection for parent DI</param>
    private static void AddPostgreSqlDatabase(this IServiceCollection services) 
    {
        services.AddOptions<PostgreSQLSettings>()
                 .Validate(s => !string.IsNullOrEmpty(s.AuthenticationDbUserConnectionString), "Missing Connection string");

        services.TryAddSingleton((IServiceProvider sp) =>
        {
            var settings = sp.GetRequiredService<IOptions<PostgreSQLSettings>>().Value;
            var connectionString = settings.AuthenticationDbUserConnectionString + ";Include Error Detail = true";
            
            // var connectionString = string.Format(
            //    settings.AuthenticationDbUserConnectionString,
            //    settings.AuthenticationDbPassword);

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.EnableDynamicJson();
            builder.MapEnum<SystemUserType>("business_application.systemuser_type");
            builder.MapEnum<SystemChangeType>("business_application.systemchange_type");
            builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
            return builder.Build();            
        });

        services.TryAddTransient((IServiceProvider sp) => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection());
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
}
