using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <summary>
/// SystemUser Repository.
/// </summary>
[ExcludeFromCodeCoverage]
internal class SystemUserRepository : ISystemUserRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger _logger;
    
    /// <summary>
    /// SystemUserRepository Constructor
    /// </summary>
    /// <param name="dataSource">Holds the Postgres db datasource</param>
    /// <param name="logger">Holds the ref to the Logger</param>
    public SystemUserRepository(
        NpgsqlDataSource dataSource,
        ILogger<SystemUserRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetDeleteSystemUserById(Guid id)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE altinn_authentication_integration.system_user_integration
	            SET is_deleted = TRUE
        	    WHERE altinn_authentication_integration.system_user_integration.system_user_integration_id = @system_user_integration_id;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_user_integration_id", id);

            await command.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToBoolean)   
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // SetDeleteSystemUserById // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<SystemUser>> GetAllActiveSystemUsersForParty(int partyId)
    {
        const string QUERY = /*strpsql*/@"
                SELECT 
                    system_user_integration_id,
		            integration_title,
		            system_internal_id,
		            owned_by_party_id,
		            created                    
                FROM altinn_authentication_integration.system_user_integration sui 
	            WHERE sui.owned_by_party_id = @owned_by_party_id	
	                  AND sui.is_deleted = false;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("owned_by_party_id", partyId.ToString());

            IAsyncEnumerable<NpgsqlDataReader> list = command.ExecuteEnumerableAsync();
            return await list.SelectAwait(ConvertFromReaderToSystemUser).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetAllActiveSystemUsersForParty // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SystemUser?> GetSystemUserById(Guid id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
	    	    system_user_integration_id,
		        integration_title,
		        system_internal_id,
		        owned_by_party_id,
		        created
	        FROM altinn_authentication_integration.system_user_integration sui 
	        WHERE sui.system_user_integration_id = @system_user_integration_id
	            AND sui.is_deleted = false;
            ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("system_user_integration_id", id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemUser)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetSystemUserById // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Guid?> InsertSystemUser(SystemUser toBeInserted)
    {
        string? system_internal_id = await ResolveSystemInternalIdFromSystemName(toBeInserted.SystemName);
        if (system_internal_id != null) 
        {
            return null;
        }

        const string QUERY = /*strpsql*/@"            
                INSERT INTO altinn_authentication_integration.system_user_integration(
                    integration_title,
                    system_internal_id,
                    owned_by_party_id,)
                VALUES(
                    @integration_title,
                    @system_internal_id,
                    @owned_by_party_id)
                RETURNING system_user_integration_id;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("integration_title", toBeInserted.IntegrationTitle);
            command.Parameters.AddWithValue("system_internal_id", system_internal_id);
            command.Parameters.AddWithValue("owned_by_party_id", toBeInserted.OwnedByPartyId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToGuid)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // InsertSystemUser // Exception");
            throw;
        }
    }

    private async Task<string?> ResolveSystemInternalIdFromSystemName(string systemId)
    {
        const string QUERY = /*strspsql*/@"
            SELECT
              hidden_internal_id
            FROM altinn_authentication_integration.system_register sr
            WHERE sr.system_id = @systemId;
        ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("systemId", systemId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemId)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // ResolveSystemInternalIdFromSystemName // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> UpdateProductName(Guid guid, string productName)
    {
        const string QUERY = /*strspsql*/@"
                UPDATE altinn_authentication_integration.system_user_integration
                SET product_name = @product_name
                WHERE system_user_integration_id = @id
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", guid);
            command.Parameters.AddWithValue("product_name", productName);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToInt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateProductName // Exception");

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SystemUser?> CheckIfPartyHasIntegration(string clientId, string systemProviderOrgNo, string systemUserOwnerOrgNo, CancellationToken cancellationToken)
    {
        string? system_internal_id = await ResolveSystemInternalIdFromClientId(clientId);

        if (system_internal_id == null)
        {
            return null;
        }

        const string QUERY = /*strspsql*/@"
              SELECT 
	    	    system_user_integration_id,
		        integration_title,
		        system_internal_id,
		        owned_by_party_id,
		        created
	        FROM altinn_authentication_integration.system_user_integration sui 
	        WHERE sui.owned_by_party_id = @systemUserOwnerOrgNo
	            AND sui.is_deleted = false
                AND sui.hidden_internal_id = @hidden_internal_id;
            ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("systemUserOwnerOrgNo", systemUserOwnerOrgNo);
            command.Parameters.AddWithValue("system_internal_id", system_internal_id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemUser)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CheckIfPartyHasIntegration // Exception");
            throw;
        }
    }

    private async Task<string?> ResolveSystemInternalIdFromClientId(string clientId)
    {
        const string QUERY = /*strspsql*/@"
            SELECT
              hidden_internal_id
            FROM altinn_authentication_integration.system_register sr
            WHERE @client_id = ANY (sr.client_id);
        ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("client_id", clientId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemId)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // ResolveProductNameFromClientId // Exception");
            throw;
        }
    }

    private ValueTask<string> ConvertFromReaderToSystemId(NpgsqlDataReader reader)
    {
        return new ValueTask<string>(reader.GetFieldValue<string>(0));
    }

    private ValueTask<int> ConvertFromReaderToInt(NpgsqlDataReader reader)
    {
        return new ValueTask<int>(reader.GetFieldValue<int>(0));
    }

    private ValueTask<Guid> ConvertFromReaderToGuid(NpgsqlDataReader reader)
    {
        return new ValueTask<Guid>(reader.GetFieldValue<Guid>(0));
    }

    private static ValueTask<SystemUser> ConvertFromReaderToSystemUser(NpgsqlDataReader reader)
    {
        return new ValueTask<SystemUser>(new SystemUser
        {
            Id = reader.GetFieldValue<Guid>("system_user_integration_id").ToString(),
            SystemName = reader.GetFieldValue<string>("system_internal_id"),
            OwnedByPartyId = reader.GetFieldValue<string>("owned_by_party_id"),
            IntegrationTitle = reader.GetFieldValue<string>("integration_title"),
            Created = reader.GetFieldValue<DateTime>("created")
        });
    }
}
