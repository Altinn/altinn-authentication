using System.Data;
using System.Threading;
using System.Transactions;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Constants;
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
    private readonly ISystemChangeLogRepository _systemChangeLogRepository;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="dataSource">Needs connection to a Postgres db</param>
    /// <param name="logger">The logger</param>
    /// <param name="systemChangeLogRepository"> The system change log repository</param>
    public SystemRegisterRepository(NpgsqlDataSource dataSource, ILogger<SystemRegisterRepository> logger, ISystemChangeLogRepository systemChangeLogRepository, TimeProvider timeProvider)
    {
        _datasource = dataSource;
        _logger = logger;
        _systemChangeLogRepository = systemChangeLogRepository;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>    
    public async Task<List<RegisteredSystemResponse>> GetAllActiveSystems(CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_internal_id,
                system_id,
                systemvendor_orgnumber, 
                system_name,
                name,
                description,
                is_deleted,
                client_id,
                rights,
                is_visible,
                allowedredirecturls,
                accesspackages
            FROM business_application.system_register sr
            WHERE sr.is_deleted = FALSE
            AND sr.is_visible=TRUE;";

        try
        {
            await using var conn = await _datasource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand command = conn.CreateCommand(QUERY);

            await command.PrepareAsync(cancellationToken);
            return await command.ExecuteEnumerableAsync(cancellationToken)
                .Select(ConvertFromReaderToSystemRegister)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetAllActiveSystems // Exception");
            throw;
        }
    }

    /// <inheritdoc/>    
    public async Task<List<RegisteredSystemResponse>> GetAllSystemsForVendor(string vendorOrgNumber, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_internal_id,
                system_id,
                systemvendor_orgnumber, 
                system_name,
                name,
                description,
                is_deleted,
                client_id,
                rights,
                is_visible,
                allowedredirecturls,
                accesspackages
            FROM business_application.system_register
            WHERE is_deleted = FALSE
            AND systemvendor_orgnumber = @systemvendor_orgnumber;";

        try
        {
            await using var conn = await _datasource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand command = conn.CreateCommand(QUERY);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_VENDOR_ORGNUMBER, vendorOrgNumber);

            await command.PrepareAsync(cancellationToken);
            return await command.ExecuteEnumerableAsync(cancellationToken)
                .Select(ConvertFromReaderToSystemRegister)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetAllSystemsForVendor // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<Guid?> CreateRegisteredSystem(RegisterSystemRequest toBeInserted, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.system_register(
                system_id,
                systemvendor_orgnumber,               
                client_id,
                is_visible,
                rights,
                name,
                description,
                allowedredirecturls,
                accesspackages)
            VALUES(
                @system_id,
                @systemvendor_orgnumber,                
                @client_id,
                @is_visible,
                @rights,
                @name,
                @description,
                @allowedredirecturls,
                @accesspackages)
            RETURNING system_internal_id;"
        ;

        await using NpgsqlConnection conn = await _datasource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            string? orgNumber = GetOrgNumber(toBeInserted.Vendor.ID);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, toBeInserted.Id);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_VENDOR_ORGNUMBER, (orgNumber == null) ? DBNull.Value : orgNumber);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_NAME, toBeInserted.Name);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_DESCRIPTION, toBeInserted.Description);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_CLIENTID, toBeInserted.ClientId);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_IS_VISIBLE, toBeInserted.IsVisible);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ALLOWED_REDIRECTURLS, toBeInserted.AllowedRedirectUrls.ConvertAll<string>(delegate(Uri u) { return u.ToString(); }));
            command.Parameters.Add(new(SystemRegisterFieldConstants.SYSTEM_RIGHTS, NpgsqlDbType.Jsonb) { Value = (toBeInserted.Rights == null) ? DBNull.Value : toBeInserted.Rights });
            command.Parameters.Add(new(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES, NpgsqlDbType.Jsonb) { Value = (toBeInserted.AccessPackages == null) ? DBNull.Value : toBeInserted.AccessPackages });

            Guid internalId = await command.ExecuteEnumerableAsync(cancellationToken)
                .Select(NpgSqlExtensions.ConvertFromReaderToGuid)
                .SingleOrDefaultAsync(cancellationToken);

            foreach (string id in toBeInserted.ClientId)
            {
                await CreateClient(id, internalId, conn, transaction, cancellationToken);
            }

            systemChangeLog.Created = _timeProvider.GetUtcNow();
            systemChangeLog.SystemInternalId = internalId;
            await _systemChangeLogRepository.LogChangeAsync(systemChangeLog, conn, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return internalId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateRegisteredSystem(RegisterSystemRequest updatedSystem, SystemChangeLog systemChangeLog, CancellationToken cancellationToken = default)
    {
        const string QUERY = /*strpsql*/@"
            UPDATE business_application.system_register
            SET systemvendor_orgnumber = @systemvendor_orgnumber,
                name = @name,
                description = @description,
                is_visible = @is_visible,
                rights = @rights,
                accesspackages = @accesspackages,
                last_changed = CURRENT_TIMESTAMP,
                allowedredirecturls = @allowedredirecturls,
                client_id = @client_id
            WHERE business_application.system_register.system_id = @system_id
            ";
        await using NpgsqlConnection conn = await _datasource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            string? orgNumber = GetOrgNumber(updatedSystem.Vendor.ID);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, updatedSystem.Id);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_VENDOR_ORGNUMBER, (orgNumber == null) ? DBNull.Value : orgNumber);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_NAME, updatedSystem.Name);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_DESCRIPTION, updatedSystem.Description);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_IS_VISIBLE, updatedSystem.IsVisible);
            command.Parameters.Add(new NpgsqlParameter(SystemRegisterFieldConstants.SYSTEM_RIGHTS, NpgsqlDbType.Jsonb) { Value = updatedSystem.Rights });
            command.Parameters.Add(new NpgsqlParameter(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES, NpgsqlDbType.Jsonb) { Value = updatedSystem.AccessPackages });
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ALLOWED_REDIRECTURLS, updatedSystem.AllowedRedirectUrls.ConvertAll<string>(delegate(Uri u) { return u.ToString(); }));
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_CLIENTID, updatedSystem.ClientId);

            bool isUpdated = await command.ExecuteNonQueryAsync(cancellationToken) > 0;

            await UpdateClient(updatedSystem.ClientId, updatedSystem.Id, conn, transaction, cancellationToken);

            systemChangeLog.Created = _timeProvider.GetUtcNow();
            await _systemChangeLogRepository.LogChangeAsync(systemChangeLog, conn, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return isUpdated;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<RegisteredSystemResponse?> GetRegisteredSystemById(string id, CancellationToken cancellationToken = default)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_internal_id,
                system_id,
                systemvendor_orgnumber, 
                system_name,
                name,
                description,
                is_deleted,
                client_id,
                rights,
                is_visible,
                allowedredirecturls,
                accesspackages
            FROM business_application.system_register sr
            WHERE sr.system_id = @system_id
        ";

        try
        {
            await using var conn = await _datasource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand command = conn.CreateCommand(QUERY);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, id);

            await command.PrepareAsync(cancellationToken);
            return await command.ExecuteEnumerableAsync(cancellationToken)
                .Select(ConvertFromReaderToSystemRegister)
                .FirstOrDefaultAsync(cancellationToken);
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
    public async Task<bool> SetDeleteRegisteredSystemById(string id, Guid systemInternalId, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
    {
        const string QUERY1 = /*strpsql*/@"
            UPDATE business_application.system_register
	        SET is_deleted = TRUE,
            last_changed = CURRENT_TIMESTAMP
        	WHERE business_application.system_register.system_id = @system_id;
            ";

        const string QUERY2 = /*strpsql*/@"
            DELETE FROM business_application.maskinporten_client            
            WHERE business_application.maskinporten_client.system_internal_id = @system_internal_id;
            ";

        await using NpgsqlConnection conn = await _datasource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            await using NpgsqlCommand command1 = new NpgsqlCommand(QUERY1, conn, transaction);
            command1.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, id);

            await using NpgsqlCommand command2 = new NpgsqlCommand(QUERY2, conn, transaction);
            command2.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_INTERNAL_ID, systemInternalId);

            int rowsAffected1 = await command1.ExecuteNonQueryAsync(cancellationToken);
            int rowsAffected2 = await command2.ExecuteNonQueryAsync(cancellationToken);

            systemChangeLog.Created = _timeProvider.GetUtcNow();
            await _systemChangeLogRepository.LogChangeAsync(systemChangeLog, conn, transaction, cancellationToken);

            await transaction.CommitAsync();

            return rowsAffected1 > 0 && rowsAffected2 > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
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
                if (!await reader.IsDBNullAsync(SystemRegisterFieldConstants.SYSTEM_RIGHTS))
                {
                    rights = await reader.GetFieldValueAsync<List<Right>>(SystemRegisterFieldConstants.SYSTEM_RIGHTS);
                }
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
    public async Task<List<AccessPackage>> GetAccessPackagesForRegisteredSystem(string systemId)
    {
        List<AccessPackage> accessPackages = [];

        const string QUERY = /*strpsql*/@"
            SELECT accesspackages
            FROM business_application.system_register
            WHERE business_application.system_register.system_id = @system_id;
            ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, systemId);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (!await reader.IsDBNullAsync(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES))
                {
                    accessPackages = await reader.GetFieldValueAsync<List<AccessPackage>>(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES);
                }
            }

            return accessPackages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetAccessPackagesForRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> UpdateRightsForRegisteredSystem(List<Right> rights, string systemId, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"            
            UPDATE business_application.system_register
            SET rights = @rights,
            last_changed = CURRENT_TIMESTAMP
            WHERE business_application.system_register.system_id = @system_id;
            ";

        try
        {
            await using NpgsqlConnection conn = await _datasource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, systemId);
            command.Parameters.Add(new(SystemRegisterFieldConstants.SYSTEM_RIGHTS, NpgsqlDbType.Jsonb) { Value = rights });

            bool isSuccess = await command.ExecuteNonQueryAsync(cancellationToken) > 0;

            systemChangeLog.Created = _timeProvider.GetUtcNow();
            await _systemChangeLogRepository.LogChangeAsync(systemChangeLog, conn, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return isSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateRightsForRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/> 
    public async Task<bool> UpdateAccessPackagesForRegisteredSystem(List<AccessPackage> accessPackages, string systemId, SystemChangeLog systemChangeLog, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"            
            UPDATE business_application.system_register
            SET accesspackages = @accesspackages,
            last_changed = CURRENT_TIMESTAMP
            WHERE business_application.system_register.system_id = @system_id;
            ";

        try
        {
            await using NpgsqlConnection conn = await _datasource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, systemId);
            command.Parameters.Add(new(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES, NpgsqlDbType.Jsonb) { Value = accessPackages });

            bool isSuccess = await command.ExecuteNonQueryAsync(cancellationToken) > 0;

            systemChangeLog.Created = _timeProvider.GetUtcNow();
            await _systemChangeLogRepository.LogChangeAsync(systemChangeLog, conn, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return isSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateAccessPackagesForRegisteredSystem // Exception");
            throw;
        }
    }

    private static RegisteredSystemResponse ConvertFromReaderToSystemRegister(NpgsqlDataReader reader)
    {
        string[] stringGuids = reader.GetFieldValue<string[]>(SystemRegisterFieldConstants.SYSTEM_CLIENTID);
        List<Right>? rights = reader.IsDBNull(SystemRegisterFieldConstants.SYSTEM_RIGHTS) ? null : reader.GetFieldValue<List<Right>>(SystemRegisterFieldConstants.SYSTEM_RIGHTS);
        List<AccessPackage>? accessPackages = reader.IsDBNull(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES) ? null : reader.GetFieldValue<List<AccessPackage>>(SystemRegisterFieldConstants.SYSTEM_ACCESSPACKAGES);
        List<string> clientIds = [];
        List<Uri> allowedRedirectUrls = [];

        foreach (string str in stringGuids)
        {
            if (string.IsNullOrEmpty(str))
            {
                continue;
            }

            clientIds.Add(str);
        }

        VendorInfo vendor = new()
        {
            ID = "0192:" + reader.GetFieldValue<string>(SystemRegisterFieldConstants.SYSTEM_VENDOR_ORGNUMBER),
            Authority = "iso6523-actorid-upis"
        };

        if (!reader.IsDBNull(SystemRegisterFieldConstants.SYSTEM_ALLOWED_REDIRECTURLS))
        {
            allowedRedirectUrls = reader.GetFieldValue<List<string>>(SystemRegisterFieldConstants.SYSTEM_ALLOWED_REDIRECTURLS).ConvertAll<Uri>(delegate(string u) { return new Uri(u); });
        }

        return new RegisteredSystemResponse
        {
            InternalId = reader.GetFieldValue<Guid>(SystemRegisterFieldConstants.SYSTEM_INTERNAL_ID),
            Id = reader.GetFieldValue<string>(SystemRegisterFieldConstants.SYSTEM_ID),
            Vendor = vendor,
            Name = reader.GetFieldValue<IDictionary<string, string>>(SystemRegisterFieldConstants.SYSTEM_NAME),
            Description = reader.GetFieldValue<IDictionary<string, string>>(SystemRegisterFieldConstants.SYSTEM_DESCRIPTION),
            IsDeleted = reader.GetFieldValue<bool>(SystemRegisterFieldConstants.SYSTEM_IS_DELETED),
            ClientId = clientIds,
            Rights = rights,
            IsVisible = reader.GetFieldValue<bool>(SystemRegisterFieldConstants.SYSTEM_IS_VISIBLE),
            AllowedRedirectUrls = allowedRedirectUrls,
            AccessPackages = accessPackages
        };
    }

    private static MaskinPortenClientInfo ConvertFromReaderToMaskinPortenClientInfo(NpgsqlDataReader reader)
    {
        return new MaskinPortenClientInfo
        {
            ClientId = reader.GetFieldValue<string>(SystemRegisterFieldConstants.SYSTEM_CLIENTID),
            SystemInternalId = reader.GetFieldValue<Guid>(SystemRegisterFieldConstants.SYSTEM_INTERNAL_ID)
        };
    }

    private async Task<bool> CreateClient(string clientId, Guid systemInteralId, NpgsqlConnection conn, NpgsqlTransaction transaction,  CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.maskinporten_client(
            client_id,
            system_internal_id)
            VALUES
            (@new_client_id,
             @system_internal_id)";

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);
            command.Parameters.AddWithValue("new_client_id", clientId);
            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_INTERNAL_ID, systemInteralId);
            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateClient // Exception");
            throw;
        }
    }

    private async Task UpdateClient(List<string> newClientIds, string systemId, NpgsqlConnection conn, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            RegisteredSystemResponse? systemInfo = await GetRegisteredSystemById(systemId, cancellationToken);
            List<MaskinPortenClientInfo> existingClients = await GetExistingClientIdsForSystem(systemInfo!.InternalId);
            List<string> existingClientIds = existingClients.Select(c => c.ClientId).ToList();

            List<string> clientIdsToDelete = existingClientIds.Except(newClientIds).ToList();
            List<string> clientIdsToAdd = newClientIds.Except(existingClientIds).ToList();

            if (clientIdsToDelete.Count > 0)
            {
                await DeleteMaskinportenClients(clientIdsToDelete, systemInfo.InternalId, conn, transaction, cancellationToken);
            }

            foreach (string clientId in clientIdsToAdd)
            {
                await CreateClient(clientId, systemInfo.InternalId, conn, transaction, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // UpdateClient // Exception");
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

            guidCommand.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_ID, id);

            return await guidCommand.ExecuteEnumerableAsync()
                .Select(NpgSqlExtensions.ConvertFromReaderToGuid)
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
        try
        {
            List<MaskinPortenClientInfo> clients = await GetMaskinportenClients(id);
            return clients.Count() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetClientByClientId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<MaskinPortenClientInfo>> GetMaskinportenClients(List<string> id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
            client_id,
            system_internal_id
            FROM business_application.maskinporten_client mc
            WHERE mc.client_id = ANY(array[@client_id]::text[]);
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_CLIENTID, id.ToArray());

            return await command.ExecuteEnumerableAsync()
                .Select(ConvertFromReaderToMaskinPortenClientInfo)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetClientByClientId // Exception");
            throw;
        }
    }

    private async Task<List<MaskinPortenClientInfo>> GetExistingClientIdsForSystem(Guid systemInternalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
            client_id,
            system_internal_id
            FROM business_application.maskinporten_client mc
            WHERE mc.system_internal_id = @system_internal_id;
        ";

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);

            command.Parameters.AddWithValue(SystemRegisterFieldConstants.SYSTEM_INTERNAL_ID, systemInternalId);

            return await command.ExecuteEnumerableAsync()
                .Select(ConvertFromReaderToMaskinPortenClientInfo)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // GetExistingClients // Exception");
            throw;
        }
    }

    private static string? GetOrgNumber(string vendorId)
    {
        if (!string.IsNullOrEmpty(vendorId))
        {
            string[] identityParts = vendorId.Split(':');
            if (identityParts.Length > 0 && identityParts[0] != "0192")
            {
                throw new ArgumentException("Invalid authority for the org number, unexpected ISO6523 identifier");
            }

            return identityParts[1];
        }

        return null;
    }

    private async Task DeleteMaskinportenClients(List<string> clientIds, Guid internalId, NpgsqlConnection conn, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            DELETE FROM business_application.maskinporten_client
            WHERE client_id = ANY(@client_ids) AND system_internal_id = @system_internal_id;
            ";
        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            command.Parameters.AddWithValue("client_ids", clientIds.ToArray()); // array of strings
            command.Parameters.AddWithValue("system_internal_id", internalId); // single Guid

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // DeleteMaskinportenClients // Exception");
            throw;
        }
    }
}