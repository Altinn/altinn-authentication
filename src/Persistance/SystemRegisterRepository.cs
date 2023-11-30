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
    /// Please observe that it is not this class that actually determine the column names! See the Yunicle migration script for that!
    /// </summary>
    private static class Params
    {
        internal const string HiddenInternalId = "hidden_internal_id";
        internal const string SystemTypeId = "registered_system_id";
        internal const string SystemVendor = "system_vendor";
        internal const string Description = "short_description";
        internal const string Created = "created";
        internal const string IsDeleted = "is_deleted";
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
    public async Task<List<RegisteredSystem>> GetAllActiveSystems()
    {
        const string QUERY = /*strpsql*/@"
        SELECT 
            registered_system_id,
            system_vendor, 
            short_description
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

    /// <inheritdoc/>  
    public async Task<string> CreateRegisteredSystem(RegisteredSystem toBeInserted)
    {
        const string QUERY = /*strpsql*/@"
        INSERT INTO altinn_authentication.system_register(
            registered_system_id,
            system_vendor,
            short_description)
        VALUES(
            @registered_system_id,
            @system_vendor,
            @description)
        RETURNING hidden_internal_guid;";

        CheckNameAvailableFixIfNot(toBeInserted);

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(Params.SystemTypeId, toBeInserted.SystemTypeId);
            command.Parameters.AddWithValue(Params.SystemVendor, toBeInserted.SystemVendor);
            command.Parameters.AddWithValue(Params.Description, toBeInserted.Description);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToString)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private void CheckNameAvailableFixIfNot(RegisteredSystem toBeInserted)
    {
        var alreadyExist = GetRegisteredSystemById(toBeInserted.SystemTypeId);
        if (alreadyExist is not null)
        {
            toBeInserted.SystemTypeId = toBeInserted.SystemTypeId + "_" + DateTime.Now.Millisecond.ToString();
        }
    }

    /// <inheritdoc/>  
    public Task<RegisteredSystem> GetRegisteredSystemById(string id)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/> 
    public Task<bool> SetRegisteredSystemDepricated(string id)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/> 
    public Task<bool> RenameRegisteredSystemById(string id)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/> 
    public Task<bool> SetDeleteRegisteredSystemById(string id)
    {
        throw new NotImplementedException();
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

    private static ValueTask<string> ConvertFromReaderToString(NpgsqlDataReader reader)
    {
        return new ValueTask<string>(reader.GetString(0));
    }
}
