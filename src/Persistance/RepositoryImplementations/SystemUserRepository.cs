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
                UPDATE altinn_authentication.system_user_integration
	            SET is_deleted = TRUE
        	    WHERE altinn_authentication.system_user_integration.system_user_integration_id = @system_user_integration_id;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_user_integration_id", id);

            await command.ExecuteEnumerableAsync()
                .SelectAwait(NpqSqlExtensions.ConvertFromReaderToBoolean)
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
		            product_name,
		            owned_by_party_id,
		            supplier_name,
		            supplier_org_no,
		            client_id,
		            is_deleted,
		            created                    
                FROM altinn_authentication.system_user_integration sui 
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
		        product_name,
		        owned_by_party_id,
		        supplier_name,
		        supplier_org_no,
		        client_id,
		        is_deleted,
		        created
	        FROM altinn_authentication.system_user_integration sui 
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
    public async Task<Guid> InsertSystemUser(SystemUser toBeInserted)
    {
        const string QUERY = /*strpsql*/@"            
                INSERT INTO altinn_authentication.system_user_integration(
                    integration_title,
                    product_name,
                    owned_by_party_id,
                    supplier_name,
                    supplier_org_no,
                    client_id)
                VALUES(
                    @integration_title,
                    @product_name,
                    @owned_by_party_id,
                    @supplier_name,
                    @supplier_org_no,
                    @client_id)
                RETURNING system_user_integration_id;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("integration_title", toBeInserted.IntegrationTitle);
            command.Parameters.AddWithValue("product_name", toBeInserted.ProductName);
            command.Parameters.AddWithValue("owned_by_party_id", toBeInserted.OwnedByPartyId);
            command.Parameters.AddWithValue("supplier_name", toBeInserted.SupplierName);
            command.Parameters.AddWithValue("supplier_org_no", toBeInserted.SupplierOrgNo);
            command.Parameters.AddWithValue("client_id", toBeInserted.ClientId);

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

    /// <inheritdoc />
    public async Task<int> UpdateProductName(Guid guid, string productName)
    {
        const string QUERY = /*strspsql*/@"
                UPDATE altinn_authentication.system_user_integration
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
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // InsertSystemUser // Exception");
            throw;
        }
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
            ProductName = reader.GetFieldValue<string>("product_name"),
            OwnedByPartyId = reader.GetFieldValue<string>("owned_by_party_id"),
            SupplierName = reader.GetFieldValue<string>("supplier_name"),
            SupplierOrgNo = reader.GetFieldValue<string>("supplier_org_no"),
            ClientId = reader.GetFieldValue<string>("client_id"),
            IntegrationTitle = reader.GetFieldValue<string>("integration_title"),
            Created = reader.GetFieldValue<DateTime>("created")
        });
    }
}
