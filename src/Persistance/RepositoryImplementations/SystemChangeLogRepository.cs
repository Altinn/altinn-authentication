using System.Text.Json;
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

    /// <inheritdoc/>
    public async Task<IList<SystemChangeLog>> GetChangeLogAsync(Guid systemInternalId, CancellationToken cancellationToken = default)
    {
        const string QUERY = @"
        SELECT 
            system_internal_id,
            changedby_orgnumber,
            change_type,
            changed_data,
            client_id,
            created
        FROM business_application.system_change_log
        WHERE system_internal_id = @system_internal_id
        ORDER BY created DESC;";

        var result = new List<SystemChangeLog>();

        try
        {
            await using NpgsqlCommand command = _datasource.CreateCommand(QUERY);
            command.Parameters.AddWithValue("system_internal_id", systemInternalId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string dbValue = reader.GetString(reader.GetOrdinal("change_type"));
                string enumValue = string.Concat(dbValue.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
                
                var log = new SystemChangeLog
                {
                    SystemInternalId = reader.GetGuid(reader.GetOrdinal("system_internal_id")),
                    ChangedByOrgNumber = reader.IsDBNull(reader.GetOrdinal("changedby_orgnumber"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("changedby_orgnumber")),
                    ChangeType = Enum.Parse<SystemChangeType>(enumValue, true),
                    ChangedData = JsonSerializer.Deserialize<object>(reader.GetString(reader.GetOrdinal("changed_data"))),
                    ClientId = reader.IsDBNull(reader.GetOrdinal("client_id"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("client_id")),
                    Created = reader.IsDBNull(reader.GetOrdinal("created"))
                        ? null
                        : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created"))
                };
                result.Add(log);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication // SystemChangeLogRepository // GetChangeLogAsync // Exception");
            throw;
        }

        return result;
    }
}
