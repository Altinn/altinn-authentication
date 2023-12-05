using System.Diagnostics.CodeAnalysis;

using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Configuration;
using Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

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

        return services;
    }

    private static void AddPostgreSqlDatabase(IServiceCollection services) 
    {
        services.AddOptions<PostgreSqlSettings>()
                 .Validate(s => !string.IsNullOrEmpty(s.ConnectionString), "Missing Connection string")
                 .Validate(s => !string.IsNullOrEmpty(s.AuthenticationDbPwd), "Missing db password");

        services.TryAddSingleton((IServiceProvider sp) =>
        {
            var settings = sp.GetRequiredService<IOptions<PostgreSqlSettings>>().Value;
            var connectionString = string.Format(
                settings.ConnectionString,
                settings.AuthenticationDbPwd);

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
            return builder.Build();
        });

        services.TryAddTransient((IServiceProvider sp) => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection());
    }

    private static void AddSystemUserRepository(IServiceCollection services) 
    {
        services.TryAddTransient<ISystemUserRespository, SystemUserRespository>();
    }
}
