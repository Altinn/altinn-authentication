using System.Data;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public RequestRepository(
        NpgsqlDataSource npgsqlDataSource,
        ILogger<RequestRepository> logger)
    {
        _dataSource = npgsqlDataSource;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateRequest(CreateRequestSystemUserResponse createRequest)
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
    public async Task<CreateRequestSystemUserResponse?> GetRequestByExternalReferences(ExternalRequestId externalRequestId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                request_status,
                redirect_urls
            FROM business_application.request r
            WHERE r.external_ref = @external_ref
                and r.system_id = @system_id
                and r.party_org_no = @party_org_no;";

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
    public async Task<CreateRequestSystemUserResponse?> GetRequestByInternalId(Guid internalId)
    {
        const string QUERY = /*strpsql*/@"
            SELECT 
                id,
                external_ref,
                system_id,
                party_org_no,
                rights,
                request_status,
                redirect_urls
            FROM business_application.request r
            WHERE r.id = @request_id;
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

    private static ValueTask<CreateRequestSystemUserResponse> ConvertFromReaderToRequest(NpgsqlDataReader reader)
    {
        string? redirect_url = null;

        if (!reader.IsDBNull("redirect_urls"))
        {
            redirect_url = reader.GetFieldValue<string?>("redirect_urls");
        }

        return new ValueTask<CreateRequestSystemUserResponse>(
            new CreateRequestSystemUserResponse()
            {
                Id = reader.GetFieldValue<Guid>("id"),
                ExternalRef = reader.GetFieldValue<string>("external_ref"),
                SystemId = reader.GetFieldValue<string>("system_id"),
                PartyOrgNo = reader.GetFieldValue<string>("party_org_no"),
                Rights = reader.GetFieldValue<List<Right>>("rights"),
                Status = reader.GetFieldValue<string>("request_status"),
                RedirectUrl = redirect_url
            });        
    }
}
