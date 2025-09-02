using System.Data;
using System.Data.Common;
using System.Threading;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <inheritdoc/>
public class RequestRepository : IRequestRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ISystemUserRepository _systemUserRepository;
    private readonly ILogger _logger;
    private const int REQUEST_TIMEOUT_DAYS = 10;

    /// <summary>
    /// Constructor
    /// </summary>
    public RequestRepository(
        NpgsqlDataSource npgsqlDataSource,
        ISystemUserRepository systemUserRepository,
        ILogger<RequestRepository> logger)
    {
        _dataSource = npgsqlDataSource;
        _systemUserRepository = systemUserRepository;   
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateRequest(RequestSystemResponse createRequest)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.request(
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                systemuser_type,
                redirect_urls)
            VALUES(
                @id,
                @external_ref,
                @system_id,
                @party_org_no,
                @rights,
                @accesspackages,
                @status,
                @systemuser_type,
                @redirect_urls);";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", createRequest.Id);
            command.Parameters.AddWithValue("external_ref", createRequest.ExternalRef!);
            command.Parameters.AddWithValue("system_id", createRequest.SystemId);
            command.Parameters.AddWithValue("party_org_no", createRequest.PartyOrgNo);
            command.Parameters.Add(new("rights", NpgsqlDbType.Jsonb) { Value = createRequest.Rights });
            command.Parameters.Add(new("accesspackages", NpgsqlDbType.Jsonb) { Value = createRequest.AccessPackages });
            command.Parameters.AddWithValue("status", createRequest.Status);

            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Standard;

            if (createRequest.RedirectUrl is not null)
            {
                command.Parameters.Add(new("redirect_urls", NpgsqlDbType.Varchar) { Value = createRequest.RedirectUrl });
            }
            else
            {
                command.Parameters.Add(new("redirect_urls", NpgsqlDbType.Varchar) { Value = DBNull.Value });
            }            

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // CreateRequest // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateAgentRequest(AgentRequestSystemResponse createAgentRequest)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.request(
                id,
                external_ref,
                system_id,
                party_org_no,
                accesspackages,
                request_status,
                systemuser_type,
                redirect_urls)
            VALUES(
                @id,
                @external_ref,
                @system_id,
                @party_org_no,
                @accessPackages,
                @status,
                @systemuser_type,
                @redirect_urls);";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", createAgentRequest.Id);
            command.Parameters.AddWithValue("external_ref", createAgentRequest.ExternalRef!);
            command.Parameters.AddWithValue("system_id", createAgentRequest.SystemId);
            command.Parameters.AddWithValue("party_org_no", createAgentRequest.PartyOrgNo);
            command.Parameters.Add(new("accesspackages", NpgsqlDbType.Jsonb) { Value = createAgentRequest.AccessPackages });
            command.Parameters.AddWithValue("status", createAgentRequest.Status);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Agent;

            if (createAgentRequest.RedirectUrl is not null)
            {
                command.Parameters.Add(new("redirect_urls", NpgsqlDbType.Varchar) { Value = createAgentRequest.RedirectUrl });
            }
            else
            {
                command.Parameters.Add(new("redirect_urls", NpgsqlDbType.Varchar) { Value = DBNull.Value });
            }

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // CreateAgentRequest // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<RequestSystemResponse?> GetRequestByExternalReferences(ExternalRequestId externalRequestId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.external_ref = @external_ref
                and r.system_id = @system_id
                and r.party_org_no = @party_org_no
                and r.is_deleted = false
                and r.systemuser_type = @systemuser_type;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("external_ref", externalRequestId.ExternalRef);
            command.Parameters.AddWithValue("system_id", externalRequestId.SystemId);
            command.Parameters.AddWithValue("party_org_no", externalRequestId.OrgNo);

            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Standard;

            var dbres = await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToRequest)
                .FirstOrDefaultAsync();
            return dbres;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetRequestByInternalId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AgentRequestSystemResponse?> GetAgentRequestByExternalReferences(ExternalRequestId externalRequestId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                accesspackages,
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.external_ref = @external_ref
                and r.system_id = @system_id
                and r.party_org_no = @party_org_no
                and r.is_deleted = false
                and r.systemuser_type = @systemuser_type ;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("external_ref", externalRequestId.ExternalRef);
            command.Parameters.AddWithValue("system_id", externalRequestId.SystemId);
            command.Parameters.AddWithValue("party_org_no", externalRequestId.OrgNo);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Agent;

            var dbres = await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToAgentRequest)
                .FirstOrDefaultAsync();
            return dbres;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetAgentRequestByExternalReferences // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<RequestSystemResponse?> GetRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                redirect_urls,
                created 
            FROM business_application.request r
            WHERE r.id = @request_id
                and r.is_deleted = false
                and r.systemuser_type = @systemuser_type;
        ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("request_id", internalId);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Standard;

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToRequest)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetRequestByInternalId // Exception"); 
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AgentRequestSystemResponse?> GetAgentRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                redirect_urls,
                created 
            FROM business_application.request r
            WHERE r.id = @request_id
                and r.is_deleted = false
                and r.systemuser_type = @systemuser_type;
        ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("request_id", internalId);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Agent;

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToAgentRequest)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetAgentRequestByInternalId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> ApproveAndCreateSystemUser(Guid requestId, Guid systemUserId, int userId, CancellationToken cancellationToken = default)
    {
        // TODO for refactor: add systemUserId column to the Request table
        string changed_by = "userId:" + userId.ToString();

        const string QUERY = /*strpsql*/"""
            UPDATE business_application.request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP,
                changed_by = @changed_by
            WHERE business_application.request.id = @requestId
            """;
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn); 

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Accepted.ToString());
            command.Parameters.AddWithValue("changed_by", changed_by);

            bool isUpdated = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
                        
            return true;
        }
        catch (Exception ex)
        {            
            _logger.LogError(ex, "Authentication // RequestRepository // ApproveAndCreateSystemUser // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> RejectSystemUser(Guid requestId, int userId, CancellationToken cancellationToken = default)
    {
        string changed_by = "userId:" + userId.ToString();

        const string QUERY = /*strpsql*/"""
            UPDATE business_application.request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP,
                changed_by = @changed_by
            WHERE business_application.request.id = @requestId
            """;       

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Rejected.ToString());
            command.Parameters.AddWithValue("changed_by", changed_by);

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // Reject System user request // Exception");
            throw;
        }
    }

    private static ValueTask<RequestSystemResponse> ConvertFromReaderToRequest(NpgsqlDataReader reader)
    {
        string? redirect_url = null;

        if (!reader.IsDBNull("redirect_urls"))
        {
            redirect_url = reader.GetFieldValue<string?>("redirect_urls");
        }

        RequestSystemResponse response = new()
        {
            Id = reader.GetFieldValue<Guid>("id"),
            ExternalRef = reader.GetFieldValue<string>("external_ref"),
            SystemId = reader.GetFieldValue<string>("system_id"),
            PartyOrgNo = reader.GetFieldValue<string>("party_org_no"),
            Rights = reader.IsDBNull("rights") ? [] : reader.GetFieldValue<List<Right>>("rights"),
            AccessPackages = reader.IsDBNull("accesspackages") ? [] : reader.GetFieldValue<List<AccessPackage>>("accesspackages"),
            Status = reader.GetFieldValue<string>("request_status"),
            Created = reader.GetFieldValue<DateTime>("created"),
            RedirectUrl = redirect_url
        };

        if (response.Created < DateTime.UtcNow.AddDays(-REQUEST_TIMEOUT_DAYS))
        {
            response.Status = RequestStatus.Timedout.ToString();
        }

        return new ValueTask<RequestSystemResponse>(response);
    }

    private static ValueTask<AgentRequestSystemResponse> ConvertFromReaderToAgentRequest(NpgsqlDataReader reader)
    {
        string? redirect_url = null;

        if (!reader.IsDBNull("redirect_urls"))
        {
            redirect_url = reader.GetFieldValue<string?>("redirect_urls");
        }

        AgentRequestSystemResponse response = new()
        {
            Id = reader.GetFieldValue<Guid>("id"),
            ExternalRef = reader.GetFieldValue<string>("external_ref"),
            SystemId = reader.GetFieldValue<string>("system_id"),
            PartyOrgNo = reader.GetFieldValue<string>("party_org_no"),
            AccessPackages = reader.IsDBNull("accesspackages") ? [] : reader.GetFieldValue<List<AccessPackage>>("accesspackages"),
            Status = reader.GetFieldValue<string>("request_status"),
            Created = reader.GetFieldValue<DateTime>("created"),
            RedirectUrl = redirect_url
        };

        if (response.Created < DateTime.UtcNow.AddDays(-REQUEST_TIMEOUT_DAYS))
        {
            response.Status = RequestStatus.Timedout.ToString();
        }

        return new ValueTask<AgentRequestSystemResponse>(response);
    }

    /// <inheritdoc/>  
    public async Task<List<RequestSystemResponse>> GetAllRequestsBySystem(string systemId, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.system_id = @system_id
                and r.is_deleted = false
                and systemuser_type = @systemuser_type;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Standard;

            return await command.ExecuteEnumerableAsync(cancellationToken)
                .SelectAwait(ConvertFromReaderToRequest)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetAllRequestsBySystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<List<AgentRequestSystemResponse>> GetAllAgentRequestsBySystem(string systemId, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                accesspackages,
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.system_id = @system_id
                and r.is_deleted = false
                and systemuser_type = @systemuser_type;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);
            command.Parameters.Add<SystemUserType>("systemuser_type").TypedValue = SystemUserType.Agent;

            return await command.ExecuteEnumerableAsync(cancellationToken)
                .SelectAwait(ConvertFromReaderToAgentRequest)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetAllAgentRequestsBySystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> DeleteRequestByRequestId(Guid requestId)
    {
        const string QUERY = /*strpsql*/"""          
            UPDATE business_application.request
            SET is_deleted = true,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.id = @requestId
                and business_application.request.is_deleted = false
            """;

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("requestId", requestId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // DeleteRequestByRequestId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> SetDeleteTimedoutRequests(int days)
    {
        const string QUERY = /*strpsql*/"""
            UPDATE business_application.request
            SET is_deleted = true,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.created < @archive_timeout
                and business_application.request.is_deleted = false
            """;

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("archive_timeout", DateTime.UtcNow.AddDays(-days));

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // DeleteTimedoutRequests // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CopyOldRequestsToArchive(int days)
    {
        const string QUERY = /*strpsql*/"""
            INSERT INTO business_application.request_archive
            SELECT *
            FROM business_application.request
            WHERE business_application.request.created < @archive_timeout
                and business_application.request.is_deleted = true;
            """;
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);        

            command.Parameters.AddWithValue("archive_timeout", DateTime.UtcNow.AddDays(-days));

            int res = await command.ExecuteNonQueryAsync();   
            
            int res2 = await DeleteArchivedAndDeleted(-days);

            if (res != res2)
            {
                await transaction.RollbackAsync();
            }

            await transaction.CommitAsync();

            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // MoveToArchive // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteArchivedAndDeleted(int days)
    {
        const string QUERY = /*strpsql*/"""
            DELETE FROM business_application.request
            WHERE business_application.request.is_deleted = true
                and business_application.request.created < @archive_timeout
            """;

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("archive_timeout", DateTime.UtcNow.AddDays(-days));

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // DeleteArchivedAndDeleted // Exception");
            throw;
        }
    }

    /// <summary>
    /// Not reachable from the API
    /// </summary>
    /// <param name="internalId">The guid as it was in the main tabble</param>
    /// <returns></returns>
    public async Task<RequestSystemResponse?> GetArchivedRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                accesspackages,
                request_status,
                redirect_urls,
                created 
            FROM business_application.request_archive r
            WHERE r.id = @request_id
                and r.is_deleted = true;
        ";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("request_id", internalId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToRequest)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetRequestByInternalId // Exception");
            throw;
        }
    }

    private ValueTask<bool> FilterTimedOut(RequestSystemResponse response)
    {
        if (response.Status == RequestStatus.Timedout.ToString())
        {
            return new ValueTask<bool>(false);
        }

        return new ValueTask<bool>(true);
    }
}
