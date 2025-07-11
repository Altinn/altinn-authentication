using System.Text.Json;
using Altinn.Platform.Authentication.Core.Enums;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations;

/// <summary>
/// Repository for logging changes to system data.
/// </summary>
public class SystemChangeLogRepository : ISystemChangeLogRepository
{
    private readonly NpgsqlDataSource _datasource;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemChangeLogRepository"/> class.
    /// </summary>
    /// <param name="datasource">the data source</param>
    /// <param name="logger">reference to logging service</param>
    public SystemChangeLogRepository(NpgsqlDataSource datasource, ILogger<SystemChangeLogRepository> logger)
    {
        _datasource = datasource;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogChangeAsync(SystemChangeLog systemChangeLog, NpgsqlConnection conn, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        const string QUERY = @"
            INSERT INTO business_application.system_change_log (
                system_internal_id,
                changedby_orgnumber,
                change_type,
                changed_data,
                client_id,
                created
            ) VALUES (
                @system_internal_id,
                @changedby_orgnumber,
                @change_type,
                @changed_data,
                @client_id,
                @created
            );";

        try
        {
            await using NpgsqlCommand command = new NpgsqlCommand(QUERY, conn, transaction);

            command.Parameters.AddWithValue("system_internal_id", systemChangeLog.SystemInternalId);
            command.Parameters.AddWithValue("changedby_orgnumber", (object?)systemChangeLog.ChangedByOrgNumber ?? DBNull.Value);
            command.Parameters.Add<SystemChangeType>("change_type").TypedValue = systemChangeLog.ChangeType;            
            command.Parameters.Add(new NpgsqlParameter("changed_data", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(systemChangeLog.ChangedData)
            });
            command.Parameters.AddWithValue("client_id", (object?)systemChangeLog.ClientId ?? DBNull.Value);
            command.Parameters.AddWithValue("created", NpgsqlTypes.NpgsqlDbType.TimestampTz, systemChangeLog.Created.Value.ToOffset(TimeSpan.Zero));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemChangeLogRepository // LogChangeAsync // Exception");
            throw;
        }
    }
}
