using System.Data;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.RepositoryInterfaces;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance;

/// <summary>
/// The System Register Repository
/// </summary>
internal class SystemRegisterRepository : ISystemRegisterRepository
{
    private readonly NpgsqlDataSource _datasource;

    /// <summary>
    /// Helper class which remembers the model's field names' mapping to the implemented Column names in the database, to ease with typing SQL commands and avoid typos.
    /// Please observe that it is not this class that actually determine the column names!
    /// </summary>
    private static class Params
    {
        internal const string SystemTypeId = "registered_system_id";
        internal const string SystemVendor = "system_vendor";
        internal const string Description = "description";
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="dataSource">Needs connection to a Postgres db</param>
    public SystemRegisterRepository(NpgsqlDataSource dataSource)
    {
        _datasource = dataSource;
    }
    
    /// <inheritdoc/>    
    public async Task<List<RegisteredSystem>> GetAllSystems()
    {
        const string QUERY = /*strpsql*/@"
        SELECT 
            registered_system_id,
            system_vendor, 
            description
        FROM altinn_authentication.system_register sr
        WHERE sr.is_deleted = FALSE;
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemRegister)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static ValueTask<RegisteredSystem> ConvertFromReaderToSystemRegister(NpgsqlDataReader reader)
    {
        return new ValueTask<RegisteredSystem>(new RegisteredSystem
        {
            SystemTypeId = reader.GetFieldValue<string>(Params.SystemTypeId),
            SystemVendor = reader.GetFieldValue<string>(Params.SystemVendor),
            Description = reader.GetFieldValue<string>(Params.Description)
        });
    }
}
