using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    public async Task<List<RegisteredSystem>> GetAllActiveSystems()
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                registered_system_id,
                system_vendor, 
                friendly_product_name,
                is_deleted,
                client_id,
                default_rights
            FROM altinn_authentication_integration.system_register sr
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
    public async Task<Guid?> CreateRegisteredSystem(RegisteredSystem toBeInserted, string[] defaultRights)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO altinn_authentication_integration.system_register(
                registered_system_id,
                system_vendor,
                friendly_product_name,
                default_rights,
                client_id)
            VALUES(
                @registered_system_id,
                @system_vendor,
                @description,
                @default_rights,
                @client_id)
            RETURNING hidden_internal_id;";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("registered_system_id", toBeInserted.SystemTypeId);
            command.Parameters.AddWithValue("system_vendor", toBeInserted.SystemVendor);
            command.Parameters.AddWithValue("description", toBeInserted.FriendlyProductName);
            command.Parameters.AddWithValue("default_rights", defaultRights);
            command.Parameters.AddWithValue("client_id", toBeInserted.ClientId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToGuid)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<RegisteredSystem?> GetRegisteredSystemById(string id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                registered_system_id,
                system_vendor, 
                friendly_product_name,
                is_deleted,
                client_id,
                default_rights
            FROM altinn_authentication_integration.system_register sr
            WHERE sr.registered_system_id = @registered_system_id;
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("registered_system_id", id);

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
    public async Task<int> RenameRegisteredSystemByGuid(Guid id, string newName)
    {
        const string UPDATEQUERY = /*strpsql*/@"
                UPDATE altinn_authentication_integration.system_register
	            SET registered_system_id = @newName
        	    WHERE altinn_authentication_integration.system_register.hidden_internal_id = @guid
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(UPDATEQUERY);

            command.Parameters.AddWithValue("guid", id);
            command.Parameters.AddWithValue("newName", newName);

            return await command.ExecuteNonQueryAsync();
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
                UPDATE altinn_authentication_integration.system_register
	            SET is_deleted = TRUE
        	    WHERE altinn_authentication_integration.system_register.registered_system_id = @registered_system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("registered_system_id", id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToBoolean)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // SetDeleteRegisteredSystemById // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<List<DefaultRight>> GetDefaultRightsForRegisteredSystem(string systemId)
    {
        const string QUERY = /*strpsql*/@"
                SELECT unnest default_rights
                FROM altinn_authentication_integration.system_register
                WHERE altinn_authentication_integration.system_register.registered_system_id = @registered_system_id;
                ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("registered_system_id", systemId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToDefaultRights)
                .ToListAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetDefaultRightsForRegisteredSystem // Exception");
            throw;
        }
    }

    /// <summary>
    /// The list of DefaultRight for each Registered System is stored as a text array in the db.
    /// Each element in this Array Type is a concatenation of the Servive Provider ( NAV, Skatteetaten, etc ...)
    /// and the Right joined with an underscore.
    /// This is to avoid a two dimensional array in the db, this is safe and easier since
    /// each Right is always in the context of it's parent Service Provider anyway.
    /// The Right can either denote a single Right or a package of Rights; which is handled in Access Management.
    /// </summary>
    private ValueTask<DefaultRight> ConvertFromReaderToDefaultRights(NpgsqlDataReader reader)
    {
        string[] arrayElement = reader.GetFieldValue<string>("default_right").Split('_');

        return new ValueTask<DefaultRight>(new DefaultRight
        {
            ServiceProvider = arrayElement[0],
            ActionRight = arrayElement[1]
        });
    }

    private static ValueTask<RegisteredSystem> ConvertFromReaderToSystemRegister(NpgsqlDataReader reader)
    {
        string[] stringGuids = reader.GetFieldValue<string[]>("client_id");
        List<Guid> clientIds = [];

        foreach (string str in stringGuids)
        {
            if (string.IsNullOrEmpty(str))
            {
                continue;
            }                                        

            clientIds.Add(Guid.Parse(str));
        }
        
        return new ValueTask<RegisteredSystem>(new RegisteredSystem
        {
            SystemTypeId = reader.GetFieldValue<string>("registered_system_id"),
            SystemVendor = reader.GetFieldValue<string>("system_vendor"),
            FriendlyProductName = reader.GetFieldValue<string>("friendly_product_name"),
            SoftDeleted = reader.GetFieldValue<bool>("is_deleted"),
            ClientId = clientIds,
            DefaultRights = ReadDefaultRightsFromDb(reader.GetFieldValue<string[]>("default_rights"))
        });
    }

    private static List<DefaultRight> ReadDefaultRightsFromDb(string[] arrayDefaultRights)
    {
        var list = new List<DefaultRight>();

        foreach (string arrayElement in arrayDefaultRights)
        {
            var subElement = arrayElement.Split("=");

            DefaultRight def = new()
                { 
                    ServiceProvider = string.Empty, // will be enriched later in the chain, is not always a part of the URN
                    ActionRight = "All", // This will be changed in a later delivery, for now, all rights are needed
                    Resources =
                    [
                        new() 
                        {
                            Id = subElement[0],
                            Value = subElement[1]
                        }
                    ]
                 
                };
            list.Add(def);
        }

        return list;
    }

    /// <inheritdoc/> 
    public async Task<bool> CreateClient(string clientId)
    {
        Guid insertedId = Guid.Parse(clientId);

        const string QUERY = /*strpsql*/@"
            INSERT INTO altinn_authentication_integration.maskinporten_client(
            client_id)
            VALUES
            (@new_client_id)";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("new_client_id", insertedId);
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
                SELECT hidden_internal_id
                FROM altinn_authentication_integration.system_register
        	    WHERE altinn_authentication_integration.system_register.registered_system_id = @registered_system_id;
                ";
    
        try
        {
            await using NpgsqlCommand guidCommand = _datasource.CreateCommand(GUIDQUERY);

            guidCommand.Parameters.AddWithValue("registered_system_id", id);

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
}
