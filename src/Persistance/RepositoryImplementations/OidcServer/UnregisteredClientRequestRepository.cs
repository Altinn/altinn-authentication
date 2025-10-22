using System.Net;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Provides methods for managing unregistered_client requests in the OIDC server database.
    /// </summary>
    /// <remarks>This repository is responsible for creating, updating, retrieving, and deleting unregistered_client
    /// request records. It uses an <see cref="NpgsqlDataSource"/> for database access and a <see cref="TimeProvider"/>
    /// to ensure consistent time-based operations. All operations are asynchronous and designed to work with
    /// PostgreSQL.</remarks>
    public sealed class UnregisteredClientRequestRepository(NpgsqlDataSource dataSource, TimeProvider timeProvider)
        : IUnregisteredClientRepository
    {
        private readonly NpgsqlDataSource _dataSource = dataSource;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Inserts a new unregistered_client request into the database with a status of 'pending'.
        /// </summary>
        public async Task InsertAsync(UnregisteredClientRequestCreate create, CancellationToken ct)
        {
            // Use TimeProvider for created_at; don't rely on DB clock
            DateTimeOffset createdAt = _timeProvider.GetUtcNow();

            const string sql = @"
            INSERT INTO oidcserver.unregistered_client_request
            (request_id, status, created_at, expires_at, issuer, goto_url, created_by_ip, user_agent_hash, correlation_id)
            VALUES (@request_id, 'pending', @created_at, @expires_at, @issuer, @goto_url, @created_by_ip, @user_agent_hash, @correlation_id);";

            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            // Explicit types for timestamptz
            cmd.Parameters.Add(new NpgsqlParameter("request_id", NpgsqlDbType.Uuid) { Value = create.RequestId });
            cmd.Parameters.Add(new NpgsqlParameter("created_at", NpgsqlDbType.TimestampTz) { Value = createdAt });
            cmd.Parameters.Add(new NpgsqlParameter("expires_at", NpgsqlDbType.TimestampTz) { Value = create.ExpiresAt });
            cmd.Parameters.Add(new NpgsqlParameter("issuer", NpgsqlDbType.Text) { Value = create.Issuer });
            cmd.Parameters.Add(new NpgsqlParameter("goto_url", NpgsqlDbType.Text) { Value = create.GotoUrl });

            // INET can be null
            cmd.Parameters.Add(new NpgsqlParameter("created_by_ip", NpgsqlDbType.Inet)
            {
                Value = (object?)create.CreatedByIp ?? DBNull.Value
            });

            cmd.Parameters.Add(new NpgsqlParameter("user_agent_hash", NpgsqlDbType.Text)
            {
                Value = (object?)create.UserAgentHash ?? DBNull.Value
            });

            cmd.Parameters.Add(new NpgsqlParameter("correlation_id", NpgsqlDbType.Uuid)
            {
                Value = (object?)create.CorrelationId ?? DBNull.Value
            });

            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// Gets a unregistered_client request by its unique request ID.
        /// </summary>
        public async Task<UnregisteredClientRequest?> GetByRequestIdAsync(Guid requestId, CancellationToken ct)
        {
            const string sql = @"
            SELECT request_id, status, created_at, expires_at, completed_at,
                    issuer, goto_url, upstream_request_id, created_by_ip, user_agent_hash, correlation_id, handled_by_callback
                FROM oidcserver.unregistered_client_request
                WHERE request_id = @request_id;";

            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("request_id", NpgsqlDbType.Uuid) { Value = requestId });

            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            if (!await rdr.ReadAsync(ct))
            {
                return null;
            }

            return Map(rdr);
        }

        /// <summary>
        /// Marks the status of a unregistered_client request. If the new status is terminal (completed, cancelled, error),
        /// </summary>
        public async Task<bool> MarkStatusAsync(Guid requestId, UnregisteredClientRequestStatus newStatus, DateTimeOffset whenUtc, string? handledByCallback, CancellationToken ct)
        {
            // Use provided 'whenUtc' (from TimeProvider) for completed_at
            const string sql = @"
            UPDATE oidcserver.unregistered_client_request
               SET status = @status,
                   completed_at = CASE
                       WHEN @status IN ('completed','cancelled','error') THEN @when_utc
                       ELSE completed_at
                   END,
                   handled_by_callback = COALESCE(@handled_by_callback, handled_by_callback)
             WHERE request_id = @request_id
               AND (
                    status = 'pending'           -- normal transition
                    OR status = @status          -- idempotent
               );";

            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("request_id", NpgsqlDbType.Uuid) { Value = requestId });
            cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = ToDb(newStatus) });
            cmd.Parameters.Add(new NpgsqlParameter("when_utc", NpgsqlDbType.TimestampTz) { Value = whenUtc });
            cmd.Parameters.Add(new NpgsqlParameter("handled_by_callback", NpgsqlDbType.Text)
            {
                Value = (object?)handledByCallback ?? DBNull.Value
            });

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows == 1;
        }

        /// <summary>
        /// Sweeps (hard-deletes) expired unregistered_client requests in small batches to avoid long transactions.
        /// </summary>
        public async Task<int> SweepExpiredAsync(DateTimeOffset nowUtc, int limit, CancellationToken ct)
        {
            // Use provided 'nowUtc' (from TimeProvider); delete in small batches
            const string sql = @"
            WITH dead AS (
                SELECT request_id
                  FROM oidcserver.unregistered_client_request
                 WHERE expires_at < @now
                 ORDER BY expires_at
                 LIMIT @lim
            )
            DELETE FROM oidcserver.unregistered_client_request cr
            USING dead
            WHERE cr.request_id = dead.request_id;";

            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = nowUtc });
            cmd.Parameters.Add(new NpgsqlParameter("lim", NpgsqlDbType.Integer) { Value = limit });

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows;
        }

        private static UnregisteredClientRequest Map(NpgsqlDataReader rdr)
        {
            var requestId = rdr.GetGuid(0);
            var status = FromDb(rdr.GetString(1));

            // created_at (timestamptz) → DateTimeOffset (UTC)
            var createdAtDt = rdr.GetFieldValue<DateTime>(2);
            var createdAt = DateTime.SpecifyKind(createdAtDt, DateTimeKind.Utc);
            var createdAtOffset = new DateTimeOffset(createdAt);

            var expiresAtDt = rdr.GetFieldValue<DateTime>(3);
            var expiresAt = new DateTimeOffset(DateTime.SpecifyKind(expiresAtDt, DateTimeKind.Utc));

            DateTimeOffset? completedAt = null;
            if (!rdr.IsDBNull(4))
            {
                var completedAtDt = rdr.GetFieldValue<DateTime>(4);
                completedAt = new DateTimeOffset(DateTime.SpecifyKind(completedAtDt, DateTimeKind.Utc));
            }

            var issuer = rdr.GetString(5);
            var gotoUrl = rdr.GetString(6);
            var upstreamRequestId = rdr.IsDBNull(7) ? (Guid?)null : rdr.GetGuid(7);

            IPAddress? ip = null;
            if (!rdr.IsDBNull(8))
            {
                ip = rdr.GetFieldValue<IPAddress>(8);
            }

            var userAgentHash = rdr.IsDBNull(9) ? null : rdr.GetString(9);
            var correlationId = rdr.IsDBNull(10) ? (Guid?)null : rdr.GetGuid(10);
            var handledByCallback = rdr.IsDBNull(11) ? null : rdr.GetString(11);

            return new UnregisteredClientRequest(
                requestId,
                status,
                createdAtOffset,
                expiresAt,
                completedAt,
                issuer,
                gotoUrl,
                upstreamRequestId,
                ip,
                userAgentHash,
                correlationId,
                handledByCallback);
        }

        private static string ToDb(UnregisteredClientRequestStatus s) => s switch
        {
            UnregisteredClientRequestStatus.Pending => "pending",
            UnregisteredClientRequestStatus.Completed => "completed",
            UnregisteredClientRequestStatus.Cancelled => "cancelled",
            UnregisteredClientRequestStatus.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
        };

        private static UnregisteredClientRequestStatus FromDb(string s) => s switch
        {
            "pending" => UnregisteredClientRequestStatus.Pending,
            "completed" => UnregisteredClientRequestStatus.Completed,
            "cancelled" => UnregisteredClientRequestStatus.Cancelled,
            "error" => UnregisteredClientRequestStatus.Error,
            _ => throw new InvalidOperationException($"Unknown status '{s}'.")
        };
    }
}
