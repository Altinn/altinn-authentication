using System.Data;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    public async Task<CreateRequestSystemUserResponse> CreateRequest(CreateRequestSystemUserResponse createRequest)
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
            command.Parameters.AddWithValue("rights", createRequest.Rights);
            command.Parameters.AddWithValue("status", createRequest.Status);
            command.Parameters.AddWithValue("redirect_urls", createRequest.RedirectUrl!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // RequestRepository // GetRequestByInternalId // Exception");
            throw;
        }

        return createRequest;
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

            command.Parameters.AddWithValue("request_id", externalRequestId.ExternalRef);
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
        return new ValueTask<CreateRequestSystemUserResponse>(
            new CreateRequestSystemUserResponse()
            {
                Id = reader.GetFieldValue<Guid>("request_id"),
                ExternalRef = reader.GetFieldValue<string>("external_ref"),
                SystemId = reader.GetFieldValue<string>("system_id"),
                PartyOrgNo = reader.GetFieldValue<string>("party_org_no"),
                Rights = reader.GetFieldValue<List<Right>>("rights"),
                Status = reader.GetFieldValue<string>("status"),
                RedirectUrl = reader.GetFieldValue<string>("redirect_urls")
            });        
    }
}
