using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <summary>
/// SystemUser Repository.
/// </summary>
[ExcludeFromCodeCoverage]
public class SystemUserRepository : ISystemUserRepository
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
                UPDATE business_application.system_user_profile
	            SET is_deleted = TRUE
        	    WHERE business_application.system_user_profile.system_user_profile_id = @system_user_profile_id;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_user_profile_id", id);

            await command.ExecuteEnumerableAsync()
                .SelectAwait(NpgSqlExtensions.ConvertFromReaderToBoolean)   
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // SetDeleteSystemUserById // Exception");            
        }
    }

    /// <inheritdoc />
    public async Task<List<SystemUser>> GetAllActiveSystemUsersForParty(int partyId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
	    	    sui.system_user_profile_id,
		        sui.integration_title,
		        sui.system_internal_id,
                sr.system_id,
                sr.systemvendor_orgnumber,
                sui.reportee_org_no,
		        sui.reportee_party_id,
		        sui.created,
                sui.external_ref,
                sui.sequence_no
	        FROM business_application.system_user_profile sui 
                JOIN business_application.system_register sr  
                ON sui.system_internal_id = sr.system_internal_id
	        WHERE sui.reportee_party_id = @reportee_party_id	
	            AND sui.is_deleted = false;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("reportee_party_id", partyId.ToString());

            IAsyncEnumerable<NpgsqlDataReader> list = command.ExecuteEnumerableAsync();
            return await list.SelectAwait(ConvertFromReaderToSystemUser).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // GetAllActiveSystemUsersForParty // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SystemUser?> GetSystemUserById(Guid id)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
	    	    sui.system_user_profile_id,
		        sui.integration_title,
		        sui.system_internal_id,
                sr.system_id,
                sr.systemvendor_orgnumber,
                sui.reportee_org_no,
		        sui.reportee_party_id,
		        sui.created,
                sui.external_ref,
                sui.sequence_no
	        FROM business_application.system_user_profile sui 
                JOIN business_application.system_register sr  
                ON sui.system_internal_id = sr.system_internal_id
	        WHERE sui.system_user_profile_id = @system_user_profile_id
	            AND sui.is_deleted = false;
            ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("system_user_profile_id", id);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemUser)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // GetSystemUserById // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SystemUser?> GetSystemUserByExternalRequestId(ExternalRequestId externalRequestId)
    {
        const string QUERY = /*strpsql*/"""
            SELECT 
                sui.system_user_profile_id,
                sui.integration_title,
                sui.system_internal_id,
                sr.system_id,
                sr.systemvendor_orgnumber,
                sui.reportee_org_no,
                sui.reportee_party_id,
                sui.created,
                sui.external_ref,
                sui.sequence_no
            FROM business_application.system_user_profile sui 
                JOIN business_application.system_register sr  
                ON sui.system_internal_id = sr.system_internal_id
            WHERE sui.external_ref = @external_ref
                and sr.system_id = @system_id
                and sui.reportee_org_no = @reportee_org_no
                and sui.is_deleted = false; 
            """;

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("external_ref", externalRequestId.ExternalRef);
            command.Parameters.AddWithValue("system_id", externalRequestId.SystemId);
            command.Parameters.AddWithValue("reportee_org_no", externalRequestId.OrgNo);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemUser)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // GetSystemUserById // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Guid?> InsertSystemUser(SystemUser toBeInserted, int userId)
    {
        const string QUERY = /*strpsql*/@"            
                INSERT INTO business_application.system_user_profile(
                    integration_title,
                    system_internal_id,
                    reportee_party_id,
                    reportee_org_no,
                    created_by,
                    external_ref)
                VALUES(
                    @integration_title,
                    @system_internal_id,
                    @reportee_party_id,
                    @reportee_org_no,
                    @created_by,
                    @external_ref)
                RETURNING system_user_profile_id;";

        string createdBy = "user_id:" + userId.ToString();
        string ext_ref = toBeInserted.ExternalRef;
        if (string.IsNullOrEmpty(ext_ref))
        {
            ext_ref = toBeInserted.ReporteeOrgNo;
        }

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("integration_title", toBeInserted.IntegrationTitle);
            command.Parameters.AddWithValue("system_internal_id", toBeInserted.SystemInternalId!);
            command.Parameters.AddWithValue("reportee_party_id", toBeInserted.PartyId);
            command.Parameters.AddWithValue("reportee_org_no", toBeInserted.ReporteeOrgNo);
            command.Parameters.AddWithValue("created_by", createdBy);
            command.Parameters.AddWithValue("external_ref", ext_ref);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToGuid)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // InsertSystemUser // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> UpdateIntegrationTitle(Guid guid, string integrationTitle)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE business_application.system_user_profile
                SET integration_title = @integration_title
                WHERE system_user_profile_id = @id
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", guid);
            command.Parameters.AddWithValue("integration_title", integrationTitle);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToInt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // UpdateProductName // Exception");

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SystemUser?> CheckIfPartyHasIntegration(
        string clientId, 
        string systemProviderOrgNo, 
        string systemUserOwnerOrgNo,
        string externalRef,
        CancellationToken cancellationToken)
    {      
        const string QUERY = /*strpsql*/@"
            SELECT 
                system_user_profile_id,
                system_id,
                integration_title,
                reportee_org_no,
                sui.system_internal_id,
                reportee_party_id,
                sui.created,
                systemvendor_orgnumber,
                external_ref,
                sui.sequence_no
            FROM business_application.system_user_profile sui
                JOIN business_application.system_register sr  
                ON   sui.system_internal_id = sr.system_internal_id
            WHERE sui.reportee_org_no = @systemUserOwnerOrgNo
                AND sui.is_deleted = false
                AND sr.is_deleted = false
                AND @client_id = ANY (sr.client_id)
                AND systemvendor_orgnumber = @systemVendorOrgno
                AND sui.external_ref = @external_ref;
            ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("systemUserOwnerOrgNo", systemUserOwnerOrgNo);
            command.Parameters.AddWithValue("client_id", clientId);
            command.Parameters.AddWithValue("systemVendorOrgno", systemProviderOrgNo);
            command.Parameters.AddWithValue("external_ref", externalRef);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToSystemUser)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // CheckIfPartyHasIntegration // Exception");
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
        string? external_ref = reader.GetFieldValue<string>("external_ref");
        string orgno = reader.GetFieldValue<string>("reportee_org_no");

        return new ValueTask<SystemUser>(new SystemUser
        {
            Id = reader.GetFieldValue<Guid>("system_user_profile_id").ToString(),
            SystemInternalId = reader.GetFieldValue<Guid>("system_internal_id"),
            SystemId = reader.GetFieldValue<string>("system_id"),
            ReporteeOrgNo = orgno,
            PartyId = reader.GetFieldValue<string>("reportee_party_id"),
            IntegrationTitle = reader.GetFieldValue<string>("integration_title"),
            Created = reader.GetFieldValue<DateTime>("created"),
            SupplierOrgNo = reader.GetFieldValue<string>("systemvendor_orgnumber"),
            ExternalRef = external_ref ?? orgno,
            SequenceNo = reader.GetFieldValue<ulong>("sequence_no")
        });
    }

    /// <inheritdoc />
    public async Task<List<SystemUser>?> GetAllSystemUsersByVendorSystem(string systemId, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
	    	    sui.system_user_profile_id,
		        sui.integration_title,
		        sui.system_internal_id,
                sr.system_id,
                sr.systemvendor_orgnumber,
                sui.reportee_org_no,
		        sui.reportee_party_id,
		        sui.created,
                sui.external_ref,
                sui.sequence_no
	        FROM business_application.system_user_profile sui 
                JOIN business_application.system_register sr  
                ON sui.system_internal_id = sr.system_internal_id
	        WHERE sr.system_id = @system_id
	            AND sui.is_deleted = false;
            ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("system_id", systemId);

            return await command.ExecuteEnumerableAsync(cancellationToken)
                .SelectAwait(ConvertFromReaderToSystemUser)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // GetAllSystemUsersByVendorSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ChangeSystemUser(SystemUser toBeChanged, int userId)
    {
        const string QUERY = /*strpsql*/@"
                UPDATE business_application.system_user_profile
	            SET changed_by = @user_id
        	    WHERE business_application.system_user_profile.system_user_profile_id = @system_user_profile_id;
                ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_user_profile_id", Guid.Parse(toBeChanged.Id));
            command.Parameters.AddWithValue("user_id", "user_id:" + userId.ToString());

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // SetDeleteSystemUserById // Exception");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<SystemUser>> GetAllSystemUsers(ulong fromSequenceNo, int limit, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
	    	    sui.system_user_profile_id,
		        sui.integration_title,
		        sui.system_internal_id,
                sr.system_id,
                sr.systemvendor_orgnumber,
                sui.reportee_org_no,
		        sui.reportee_party_id,
		        sui.created,
                sui.external_ref,
                sui.sequence_no
	        FROM business_application.system_user_profile sui
                JOIN business_application.system_register sr  
                ON sui.system_internal_id = sr.system_internal_id    
            WHERE sui.is_deleted = false
                AND sequence_no > @sequence_no
            LIMIT @limit;
            "
        ;

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("sequence_no", fromSequenceNo);
            command.Parameters.AddWithValue("limit", limit);

            return await command.ExecuteEnumerableAsync(cancellationToken)
                .SelectAwait(ConvertFromReaderToSystemUser)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemUserRepository // GetAllSystemUsers // Exception");
            throw;
        }
    }
}
