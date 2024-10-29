using System.Data;
using System.Data.Common;
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
public class ChangeRequestRepository(
    NpgsqlDataSource dataSource,
    ISystemUserRepository systemUserRepository,
    ILogger<ChangeRequestRepository> logger) : IChangeRequestRepository
{
    private readonly ILogger _logger = logger;
    private const int REQUEST_TIMEOUT_DAYS = 10;
    private const int ARCHIVE_TIMEOUT_DAYS = 60;

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateChangeRequest(ChangeRequestResponse createRequest)
    {
        const string QUERY = /*strpsql*/@"
            INSERT INTO business_application.change_request(
                id,
                external_ref,
                system_id,
                party_org_no,
                required_rights,
                unwanted_rights,
                request_status,
                redirect_urls)
            VALUES(
                @id,
                @external_ref,
                @system_id,
                @party_org_no,
                @required_rights,
                @unwanted_rights,
                @status,
                @redirect_urls);"
        ;

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("id", createRequest.Id);
            command.Parameters.AddWithValue("external_ref", createRequest.ExternalRef!);
            command.Parameters.AddWithValue("system_id", createRequest.SystemId);
            command.Parameters.AddWithValue("party_org_no", createRequest.PartyOrgNo);
            command.Parameters.Add(new("required_rights", NpgsqlDbType.Jsonb) { Value = createRequest.RequiredRights });
            command.Parameters.Add(new("unwanted_rights", NpgsqlDbType.Jsonb) { Value = createRequest.UnwantedRights });
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
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // GetChangeRequestByInternalId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ApproveAndDelegateOnSystemUser(Guid requestId, SystemUser toBeChanged, int userId, CancellationToken cancellationToken)
    {
        string changed_by = "userId:" + userId.ToString();

        const string QUERY = /*strpsql*/"""
            UPDATE business_application.change_request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP,
                changed_by = @changed_by
            WHERE business_application.request.id = @requestId
            """;
        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            await using NpgsqlCommand command = new(QUERY, conn, transaction);

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Accepted.ToString());
            command.Parameters.AddWithValue("changed_by", changed_by);

            bool isUpdated = await command.ExecuteNonQueryAsync(cancellationToken) > 0;

            var changed = await systemUserRepository.ChangeSystemUser(toBeChanged, userId);

            await transaction.CommitAsync(cancellationToken);

            return changed;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // ApproveAndDelegateOnSystemUser // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteChangeRequestByRequestId(Guid requestId)
    {
        const string QUERY = /*strpsql*/"""          
            UPDATE business_application.change_request
            SET is_deleted = true,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.id = @requestId
                and business_application.request.is_deleted = false
            """;

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("requestId", requestId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // DeleteChangeRequestByRequestId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteTimedoutChangeRequests()
    {
        const string QUERY = /*strpsql*/"""
            UPDATE business_application.change_request
            SET is_deleted = true,
                last_changed = CURRENT_TIMESTAMP
            WHERE business_application.request.created < @archive_timeout
                and business_application.request.is_deleted = false
            """;

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("archive_timeout", DateTime.UtcNow.AddDays(-ARCHIVE_TIMEOUT_DAYS));

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // DeleteTimedoutChangeRequests // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<ChangeRequestResponse>> GetAllChangeRequestsBySystem(string systemId, CancellationToken cancellationToken)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                required_rights,
                unwanted_rights,
                request_status,
                redirect_urls,
                created
            FROM business_application.change_request r
            WHERE r.system_id = @system_id
                and r.is_deleted = false;";

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("system_id", systemId);

            return await command.ExecuteEnumerableAsync(cancellationToken)
                .SelectAwait(ConvertFromReaderToChangeRequest)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // GetAllChangeRequestsBySystem // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ChangeRequestResponse?> GetChangeRequestByExternalReferences(ExternalRequestId externalRequestId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                required_rights,
                unwanted_rights,
                request_status,
                redirect_urls,
                created
            FROM business_application.change_request r
            WHERE r.external_ref = @external_ref
                and r.system_id = @system_id
                and r.party_org_no = @party_org_no
                and r.is_deleted = false;";

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("external_ref", externalRequestId.ExternalRef);
            command.Parameters.AddWithValue("system_id", externalRequestId.SystemId);
            command.Parameters.AddWithValue("party_org_no", externalRequestId.OrgNo);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToChangeRequest)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // GetChangeRequestByInternalId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ChangeRequestResponse?> GetChangeRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                required_rights,
                unwanted_rights,
                request_status,
                redirect_urls,
                created 
            FROM business_application.change_request r
            WHERE r.id = @request_id
                and r.is_deleted = false;
        ";

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("request_id", internalId);

            return await command.ExecuteEnumerableAsync()
                .SelectAwait(ConvertFromReaderToChangeRequest)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // GetChangeRequestByInternalId // Exception");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RejectChangeOnSystemUser(Guid requestId, int userId, CancellationToken cancellationToken)
    {
        string changed_by = "userId:" + userId.ToString();

        const string QUERY = /*strpsql*/"""
            UPDATE business_application.change_request
            SET request_status = @request_status,
                last_changed = CURRENT_TIMESTAMP,
                changed_by = @changed_by
            WHERE business_application.request.id = @requestId
            """;

        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(QUERY);

            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("request_status", RequestStatus.Rejected.ToString());
            command.Parameters.AddWithValue("changed_by", changed_by);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // ChangeRequestRepository // Reject change on SystemUser // Exception");
            throw;
        }
    }

    private static ValueTask<ChangeRequestResponse> ConvertFromReaderToChangeRequest(NpgsqlDataReader reader)
    {
        string? redirect_url = null;

        if (!reader.IsDBNull("redirect_urls"))
        {
            redirect_url = reader.GetFieldValue<string?>("redirect_urls");
        }

        ChangeRequestResponse response = new()
        {
            Id = reader.GetFieldValue<Guid>("id"),
            ExternalRef = reader.GetFieldValue<string>("external_ref"),
            SystemId = reader.GetFieldValue<string>("system_id"),
            PartyOrgNo = reader.GetFieldValue<string>("party_org_no"),
            RequiredRights = reader.GetFieldValue<List<Right>>("required_rights"),
            UnwantedRights = reader.GetFieldValue<List<Right>>("unwanted_rights"),
            Status = reader.GetFieldValue<string>("request_status"),
            Created = reader.GetFieldValue<DateTime>("created"),
            RedirectUrl = redirect_url
        };

        if (response.Created < DateTime.UtcNow.AddDays(-REQUEST_TIMEOUT_DAYS))
        {
            response.Status = RequestStatus.Timedout.ToString();
        }

        return new ValueTask<ChangeRequestResponse>(response);
    }
}