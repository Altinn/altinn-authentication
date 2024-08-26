using System.Data;
using System.Text.Json;
using System.Text.Unicode;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <summary>
/// The System Register Repository
/// </summary>
internal class SystemRegisterRepository : ISystemRegisterRepository
{
    private readonly NpgsqlDataSource _datasource;
    private readonly ILogger _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="dataSource">Needs connection to a Postgres db</param>
    /// <param name="logger">The logger</param>
    public SystemRegisterRepository(NpgsqlDataSource dataSource, ILogger<SystemRegisterRepository> logger)
    {
        _datasource = dataSource;
        _logger = logger;
    }

    /// <inheritdoc/>    
    public async Task<List<RegisterSystemResponse>> GetAllActiveSystems()
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_internal_id,
                system_id,
                systemvendor_orgnumber, 
                system_name,
                is_deleted,
                client_id,
                rights,
                is_visible
            FROM business_application.system_register sr
            WHERE sr.is_deleted = FALSE;";

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
    public async Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest toBeInserted)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.system_register(
                system_id,
                systemvendor_orgnumber,
                system_name,
                client_id,
                is_visible,
                rights)
            VALUES(
                @system_id,
                @systemvendor_orgnumber,
                @system_name,
                @client_id,
                @is_visible,
                @rights)
            RETURNING system_internal_id;";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", toBeInserted.SystemId);
            command.Parameters.AddWithValue("systemvendor_orgnumber", toBeInserted.SystemVendorOrgNumber);
            command.Parameters.AddWithValue("system_name", toBeInserted.SystemName);
            command.Parameters.AddWithValue("client_id", toBeInserted.ClientId);
            command.Parameters.AddWithValue("is_visible", toBeInserted.IsVisible);
            command.Parameters.Add(new("rights", NpgsqlDbType.Jsonb) { Value = toBeInserted.Rights });

            Guid systemInternalId = await command.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToGuid)
                .SingleOrDefaultAsync();
            
            foreach (string id in toBeInserted.ClientId)
            {
                await CreateClient(id, systemInternalId);
            }

            return systemInternalId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> UpdateRegisteredSystem(RegisterSystemRequest updatedSystem)
    {
        const string QUERY = /*strpsql*/"""
            UPDATE business_application.system_register
            SET system_name = @system_name,
                is_visible = @is_visible,
                is_deleted = @is_deleted,
                rights = @rights
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.system_register.system_id = @system_id
            """;

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", updatedSystem.SystemId);
            command.Parameters.AddWithValue("systemvendor_orgnumber", updatedSystem.SystemVendorOrgNumber);
            command.Parameters.AddWithValue("system_name", updatedSystem.SystemName);
            command.Parameters.AddWithValue("is_visible", updatedSystem.IsVisible);
            command.Parameters.Add(new("rights", NpgsqlDbType.Jsonb) { Value = updatedSystem.Rights });

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<RegisterSystemResponse?> GetRegisteredSystemById(string id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_internal_id,
                system_id,
                systemvendor_orgnumber, 
                system_name,
                is_deleted,
                client_id,
                rights,
                is_visible
            FROM business_application.system_register sr
            WHERE sr.system_id = @system_id;
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", id);

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
    public async Task<int> RenameRegisteredSystemIdByGuid(Guid id, string systemId)
    {
        const string UPDATEQUERY = /*strpsql*/@"
                UPDATE business_application.system_register
	            SET system_id = @systemId,
                last_changed = CURRENT_TIMESTAMP
        	    WHERE business_application.system_register.system_internal_id = @guid
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(UPDATEQUERY);

            command.Parameters.AddWithValue("guid", id);
            command.Parameters.AddWithValue("systemId", systemId);

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // RenameRegisteredSystemIdById // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> SetDeleteRegisteredSystemById(string id)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE business_application.system_register
	            SET is_deleted = TRUE,
                last_changed = CURRENT_TIMESTAMP
        	    WHERE business_application.system_register.system_id = @system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // SetDeleteRegisteredSystemById // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<List<Right>> GetRightsForRegisteredSystem(string systemId)
    {
        List<Right> rights = [];

        const string QUERY = /*strpsql*/@"
                SELECT rights
                FROM business_application.system_register
                WHERE business_application.system_register.system_id = @system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rights = reader.GetFieldValue<List<Right>>("rights");                                
            }

            return rights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetRightsForRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId)
    {
        const string QUERY = /*strpsql*/"""            
            UPDATE business_application.system_register
            SET rights = @rights,
            last_changed = CURRENT_TIMESTAMP
            WHERE business_application.system_register.system_id = @system_id;
            """;

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);
            command.Parameters.Add(new("rights", NpgsqlDbType.Jsonb) { Value = rights });

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateRightsForRegisteredSystem // Exception");
            throw;
        }
    }

    /// <summary>
    /// The list of Right for each Registered System is stored as a text array in the db.
    /// Each element in this Array Type is a concatenation of the Servive Provider ( NAV, Skatteetaten, etc ...)
    /// and the Right joined with an underscore.
    /// This is to avoid a two dimensional array in the db, this is safe and easier since
    /// each Right is always in the context of it's parent Service Provider anyway.
    /// The Right can either denote a single Right or a package of Rights; which is handled in Access Management.
    /// </summary>
    private ValueTask<List<Right>> ConvertFromReaderToRights(NpgsqlDataReader reader)
    {
        List<Right> rights = reader.GetFieldValue<List<Right>>("rights");

        return new ValueTask<List<Right>>(rights);
    }

    private static ValueTask<RegisterSystemResponse> ConvertFromReaderToSystemRegister(NpgsqlDataReader reader)
    {
        string[] stringGuids = reader.GetFieldValue<string[]>("client_id");                
        List<Right> rights = reader.GetFieldValue<List<Right>>("rights");
        List<string> clientIds = [];

        foreach (string str in stringGuids)
        {
            if (string.IsNullOrEmpty(str))
            {
                continue;
            }                                        

            clientIds.Add(str);
        }
        
        return new ValueTask<RegisterSystemResponse>(new RegisterSystemResponse
        {
            SystemInternalId = reader.GetFieldValue<Guid>("system_internal_id"),
            SystemId = reader.GetFieldValue<string>("system_id"),
            SystemVendorOrgNumber = reader.GetFieldValue<string>("systemvendor_orgnumber"),
            SystemName = reader.GetFieldValue<string>("system_name"),
            SoftDeleted = reader.GetFieldValue<bool>("is_deleted"),
            ClientId = clientIds,
            Rights = rights,
            IsVisible = reader.GetFieldValue<bool>("is_visible"),
        });
    }

    /// <inheritdoc/> 
    public async Task<bool> CreateClient(string clientId, Guid systemInteralId)
    {
        Guid insertedId = Guid.Parse(clientId);

        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.maskinporten_client(
            client_id,
            system_internal_id)
            VALUES
            (@new_client_id,
             @system_internal_id)";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("new_client_id", insertedId);
            command.Parameters.AddWithValue("system_internal_id", systemInteralId);
            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateClient // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<Guid?> RetrieveGuidFromStringId(string id)
    {
        const string GUIDQUERY = /*strpsql*/@"
                SELECT system_internal_id
                FROM business_application.system_register
        	    WHERE business_application.system_register.system_id = @system_id;
                ";
    
        try
        {
            await using NpgsqlCommand guidCommand = _datasource.CreateCommand(GUIDQUERY);

            guidCommand.Parameters.AddWithValue("system_id", id);

            return await guidCommand.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToGuid)
                .SingleOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // RetrieveGuidFromStringId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> DoesClientIdExists(List<string> id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
            client_id
            FROM business_application.maskinporten_client mc
            WHERE mc.client_id = ANY(array[@client_id]::uuid[]);
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("client_id", id.ToArray());

            return await command.ExecuteEnumerableAsync()
                .CountAsync() >= 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetClientByClientId // Exception");
            throw;
        }
    }

    private static List<Right> GetRights(string[] rights)
    {
        return JsonSerializer.Deserialize<List<Right>>(rights[0]);
    }
}
