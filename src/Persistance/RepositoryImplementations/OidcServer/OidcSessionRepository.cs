using System.Data;
using System.Text.Json;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository implementation for OIDC session management.
    /// </summary>
    public sealed class OidcSessionRepository(NpgsqlDataSource ds, ILogger<OidcSessionRepository> logger, TimeProvider timeProvider) : IOidcSessionRepository
    {
        private readonly NpgsqlDataSource _ds = ds;
        private readonly ILogger<OidcSessionRepository> _logger = logger;
        private static readonly string[] EmptyAmr = Array.Empty<string>();
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Create OIDC session.
        /// </summary>
        public async Task<OidcSession> CreateSession(OidcSessionCreate create, CancellationToken cancellationToken = default)
        {
            const string SQL = /*strpsql*/ @"
                INSERT INTO oidcserver.oidc_session (
                    sid, session_handle_hash, upstream_issuer, upstream_sub, subject_id, external_id,
                    subject_party_uuid, subject_party_id, subject_user_id, subject_user_name,
                    provider, acr, auth_time, amr, scopes,
                    created_at, updated_at, last_seen_at, expires_at,
                    upstream_session_sid, created_by_ip, user_agent_hash, provider_claims
                )
                SELECT
                    @sid, @session_handle_hash, @upstream_issuer, @upstream_sub, @subject_id, @external_id,
                    @subject_party_uuid, @subject_party_id, @subject_user_id, @subject_user_name,
                    @provider, @acr, @auth_time, @amr, @scopes,
                    @now, @now, @now, @expires_at,
                    @upstream_session_sid, @created_by_ip, @user_agent_hash, @provider_claims
                RETURNING *;
            ";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("sid", create.Sid);
            cmd.Parameters.AddWithValue("session_handle_hash", NpgsqlDbType.Bytea, create.SessionHandleHash);
            cmd.Parameters.AddWithValue("subject_id", (object?)create.SubjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("external_id", (object?)create.ExternalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_uuid", (object?)create.SubjectPartyUuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_id", (object?)create.SubjectPartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_user_id", (object?)create.SubjectUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_user_name", (object?)create.SubjectUserName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("provider", create.Provider);
            cmd.Parameters.AddWithValue("upstream_issuer", create.UpstreamIssuer);
            cmd.Parameters.AddWithValue("upstream_sub", create.UpstreamSub);
            cmd.Parameters.AddWithValue("acr", (object?)create.Acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_time", (object?)create.AuthTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("amr", (object?)(create.Amr ?? EmptyAmr) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("scopes", NpgsqlDbType.Array | NpgsqlDbType.Text, create.Scopes.ToArray());
            cmd.Parameters.AddWithValue("now", (object?)create.Now ?? DBNull.Value);
            cmd.Parameters.AddWithValue("expires_at", (object?)create.ExpiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("upstream_session_sid", (object?)create.UpstreamSessionSid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_by_ip", (object?)create.CreatedByIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("user_agent_hash", (object?)create.UserAgentHash ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("provider_claims", create.ProviderClaims));

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Failed to upsert oidc_session.");
            }

            return Map(reader);
        }

        /// <summary>
        /// Get an OIDC session by its SID.
        /// </summary>
        public async Task<OidcSession?> GetBySidAsync(string sid, CancellationToken cancellationToken = default)
        {
            const string SQL = "SELECT * FROM oidcserver.oidc_session WHERE sid=@sid LIMIT 1;";
            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("sid", sid);

            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await r.ReadAsync(cancellationToken))
            {
                return null;
            }

            return Map(r);
        }

        /// <summary>
        /// Returns a sessionID by its session handle.
        /// The session handle is exposed to clients in a cookie, but the hash is stored in the database.
        /// </summary>
        public async Task<OidcSession?> GetBySessionHandleHashAsync(byte[] sessionHandleHash, CancellationToken cancellationToken = default)
        {
            const string SQL = "SELECT * FROM oidcserver.oidc_session WHERE session_handle_hash = @handle_hash LIMIT 1;";
            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("handle_hash", sessionHandleHash);

            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await r.ReadAsync(cancellationToken))
            {
                return null;
            }

            return Map(r);
        }

        /// <summary>
        /// Delete an OIDC session by its SID.
        /// </summary>
        public async Task<bool> DeleteBySidAsync(string sid, CancellationToken cancellationToken = default)
        {
            const string SQL = "DELETE FROM oidcserver.oidc_session WHERE sid=@sid;";
            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("sid", sid);
            var n = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return n > 0;
        }

        /// <summary>
        /// Slides the expiry of an OIDC session to a new value if the new value is later than the current expiry.
        /// </summary>
        public async Task<bool> SlideExpiryToAsync(string sid, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE oidcserver.oidc_session
                   SET expires_at = @new_exp,
                       updated_at = @now,
                       last_seen_at = @now
                 WHERE sid = @sid
                   AND (expires_at IS NULL OR expires_at < @new_exp);";

            var now = _timeProvider.GetUtcNow();
            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("sid", sid);
            cmd.Parameters.AddWithValue("new_exp", NpgsqlTypes.NpgsqlDbType.TimestampTz, newExpiresAt);
            cmd.Parameters.AddWithValue("now", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return rows > 0;
        }

        /// <summary>
        /// Get all SIDs for sessions matching the given upstream issuer and upstream session SID.
        /// </summary>
        public async Task<string[]> GetSidsByUpstreamSessionSidAsync(string issuer, string upstreamSid, CancellationToken cancellationToken = default)
        {
            const string SQL = @"
            SELECT sid
              FROM oidcserver.oidc_session
             WHERE upstream_issuer = @iss
               AND upstream_session_sid = @usid;";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("iss", issuer);
            cmd.Parameters.AddWithValue("usid", upstreamSid);

            var list = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(r.GetFieldValue<string>("sid"));
            }

            return list.ToArray();
        }

        /// <summary>
        /// Delete all OIDC sessions matching the given upstream issuer and upstream session SID.
        /// </summary>
        public async Task<int> DeleteByUpstreamSessionSidAsync(string issuer, string upstreamSid, CancellationToken cancellationToken = default)
        {
            const string SQL = @"
        DELETE FROM oidcserver.oidc_session
            WHERE upstream_issuer = @iss
            AND upstream_session_sid = @usid;";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("iss", issuer);
            cmd.Parameters.AddWithValue("usid", upstreamSid);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Touch the last seen timestamp of an OIDC session to the current time.
        /// </summary>
        public async Task TouchLastSeenAsync(string sid, CancellationToken cancellationToken = default)
        {
            const string sql = @"
            UPDATE oidcserver.oidc_session
               SET last_seen_at = @now,
                   updated_at   = @now
             WHERE sid = @sid;";
            var now = _timeProvider.GetUtcNow();
            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("sid", sid);
            cmd.Parameters.AddWithValue("now", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static NpgsqlParameter JsonbParam(string name, Dictionary<string, List<string>>? dict)
        {
            return new NpgsqlParameter(name, NpgsqlDbType.Jsonb)
            {
                Value = dict is null ? DBNull.Value : JsonSerializer.Serialize(dict)
            };
        }

        private static OidcSession Map(NpgsqlDataReader r)
        {
            return new OidcSession
            {
                Sid = r.GetFieldValue<string>("sid"),
                SessionHandle = (byte[])r["session_handle_hash"],
                SubjectId = r.GetFieldValue<string>("subject_id"),
                ExternalId = r.IsDBNull("external_id") ? null : r.GetFieldValue<string>("external_id"),
                SubjectPartyUuid = r.IsDBNull("subject_party_uuid") ? null : r.GetFieldValue<Guid?>("subject_party_uuid"),
                SubjectPartyId = r.IsDBNull("subject_party_id") ? null : r.GetFieldValue<int?>("subject_party_id"),
                SubjectUserId = r.IsDBNull("subject_user_id") ? null : r.GetFieldValue<int?>("subject_user_id"),
                SubjectUserName = r.IsDBNull("subject_user_name") ? null : r.GetFieldValue<string>("subject_user_name"),
                Provider = r.GetFieldValue<string>("provider"),
                UpstreamIssuer = r.GetFieldValue<string>("upstream_issuer"),
                UpstreamSub = r.GetFieldValue<string>("upstream_sub"),
                Acr = r.IsDBNull("acr") ? null : r.GetFieldValue<string>("acr"),
                AuthTime = r.IsDBNull("auth_time") ? null : r.GetFieldValue<DateTimeOffset?>("auth_time"),
                Amr = r.IsDBNull("amr") ? null : r.GetFieldValue<string[]>("amr"),
                Scopes = r.GetFieldValue<string[]>("scopes"),
                CreatedAt = r.GetFieldValue<DateTimeOffset>("created_at"),
                UpdatedAt = r.GetFieldValue<DateTimeOffset>("updated_at"),
                ExpiresAt = r.IsDBNull("expires_at") ? null : r.GetFieldValue<DateTimeOffset?>("expires_at"),
                UpstreamSessionSid = r.IsDBNull("upstream_session_sid") ? null : r.GetFieldValue<string>("upstream_session_sid"),
                LastSeenAt = r.IsDBNull("last_seen_at") ? null : r.GetFieldValue<DateTimeOffset?>("last_seen_at"),
                ProviderClaims = r.IsDBNull("provider_claims") ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(r.GetFieldValue<string>("provider_claims"))
            };
        }
    }
}
