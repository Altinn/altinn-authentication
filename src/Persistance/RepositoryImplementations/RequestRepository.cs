﻿using System.Data;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Core.Models;
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
    private const int ARCHIVE_TIMEOUT_DAYS = 60;

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
                request_status,
                redirect_urls)
            VALUES(
                @id,
                @external_ref,
                @system_id,
                @party_org_no,
                @rights,
                @status,
                @redirect_urls);";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", createRequest.Id);
            command.Parameters.AddWithValue("external_ref", createRequest.ExternalRef!);
            command.Parameters.AddWithValue("system_id", createRequest.SystemId);
            command.Parameters.AddWithValue("party_org_no", createRequest.PartyOrgNo);
            command.Parameters.Add(new("rights", NpgsqlDbType.Jsonb) { Value = createRequest.Rights });
            command.Parameters.AddWithValue("status", createRequest.Status);

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
            _logger.LogError(ex, "Authentication // RequestRepository // GetRequestByInternalId // Exception");
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
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.external_ref = @external_ref
                and r.system_id = @system_id
                and r.party_org_no = @party_org_no
                and r.is_deleted = false;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("external_ref", externalRequestId.ExternalRef);
            command.Parameters.AddWithValue("system_id", externalRequestId.SystemId);
            command.Parameters.AddWithValue("party_org_no", externalRequestId.OrgNo);

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
    public async Task<RequestSystemResponse?> GetRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                request_status,
                redirect_urls,
                created 
            FROM business_application.request r
            WHERE r.id = @request_id
                and r.is_deleted = false;
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

    /// <inheritdoc/>  
    public async Task<Guid?> ApproveAndCreateSystemUser(Guid requestId, SystemUser toBeInserted, CancellationToken cancellationToken = default)
    {
        const string QUERY = /*strpsql*/"""
            UPDATE business_application.request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.id = @requestId
            """;
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Accepted.ToString());

            bool isUpdated = await command.ExecuteNonQueryAsync() > 0;

            Guid? systemUserId = await _systemUserRepository.InsertSystemUser(toBeInserted);

            await transaction.CommitAsync();

            return systemUserId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Authentication // SystemRegisterRepository // CreateRegisteredSystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>  
    public async Task<bool> RejectSystemUser(Guid requestId, CancellationToken cancellationToken = default)
    {
        const string QUERY = /*strpsql*/"""
            UPDATE business_application.request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.id = @requestId
            """;       

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Rejected.ToString());

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
            Rights = reader.GetFieldValue<List<Right>>("rights"),
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
                request_status,
                redirect_urls,
                created
            FROM business_application.request r
            WHERE r.system_id = @system_id
                and r.is_deleted = false;";

        try
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);

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
    public async Task<int> DeleteTimedoutRequests()
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

            command.Parameters.AddWithValue("archive_timeout", DateTime.UtcNow.AddDays(-ARCHIVE_TIMEOUT_DAYS));

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // DeleteTimedoutRequests // Exception");
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
