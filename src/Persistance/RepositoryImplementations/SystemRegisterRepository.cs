using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <summary>
/// The System Register Repository
/// </summary>
[ExcludeFromCodeCoverage]
internal class SystemRegisterRepository : ISystemRegisterRepository
{
    private readonly NpgsqlDataSource _datasource;
    private readonly ILogger _logger;

    /// <summary>
    /// Helper class which remembers the model's field names' mapping to the implemented Column names in the database, to ease with typing SQL commands and avoid typos.
    /// Please observe that it is not this class that actually determine the column names! See the Yuniql migration script for that!
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
    /// <param name="logger">The logger</param>
    public SystemRegisterRepository(NpgsqlDataSource dataSource, ILogger logger)
    {
        _datasource = dataSource;
        _logger = logger;
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
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetAllActiveSystems // Exception");
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
                .SelectAwait(NpqSqlExtensions.ConvertFromReaderToString)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
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
    public async Task<RegisteredSystem> GetRegisteredSystemById(string id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                registered_system_id,
                system_vendor, 
                short_description
            FROM altinn_authentication.system_register sr
            WHERE sr.registered_system_id = @registered_system_id;
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(Params.SystemTypeId, id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemRegister)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetRegisteredSystemById // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> RenameRegisteredSystemById(string id, string newName)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE altinn_authentication.system_register
	            SET registered_system_id = @newName
        	    WHERE altinn_authentication.system_register.registered_system_id = @registered_system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(Params.SystemTypeId, id);
            command.Parameters.AddWithValue("newName", newName);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(NpqSqlExtensions.ConvertFromReaderToBoolean)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // RenameRegisteredSystemById // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> SetDeleteRegisteredSystemById(string id)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE altinn_authentication.system_register
	            SET is_deleted = TRUE
        	    WHERE altinn_authentication.system_register.registered_system_id = @registered_system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(Params.SystemTypeId, id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(NpqSqlExtensions.ConvertFromReaderToBoolean)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // SetDeleteRegisteredSystemById // Exception");
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
