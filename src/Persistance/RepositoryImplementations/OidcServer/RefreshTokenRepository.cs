using System.Data;
using System.Text.Json;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository implementation for refresh token and refresh token family management.
    /// </summary>
    public sealed class RefreshTokenRepository(NpgsqlDataSource dataSource) : IRefreshTokenRepository
    {
        private readonly NpgsqlDataSource _dataSource = dataSource;

        /// <summary>
        /// Gets an existing non-revoked family for (clientId, subjectId, opSid), or creates a new one if none exists.
        /// </summary>
        public async Task<Guid> GetOrCreateFamilyAsync(string clientId, string subjectId, string opSid, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // Try find an existing, non-revoked family for (client, subject, opSid)
            const string selectSql = @"
            SELECT family_id
            FROM oidcserver.refresh_token_family
            WHERE client_id = @client_id
              AND subject_id = @subject_id
              AND op_sid    = @op_sid
              AND revoked_at IS NULL
            LIMIT 1
            FOR UPDATE";    

            await using (var cmd = new NpgsqlCommand(selectSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("client_id", NpgsqlDbType.Text, clientId);
                cmd.Parameters.AddWithValue("subject_id", NpgsqlDbType.Text, subjectId);
                cmd.Parameters.AddWithValue("op_sid", NpgsqlDbType.Text, opSid);

                var result = await cmd.ExecuteScalarAsync(ct);
                if (result is Guid existingId)
                {
                    await tx.CommitAsync(ct);
                    return existingId;
                }
            }

            // Create a new family
            Guid familyId = Guid.NewGuid();
            const string insertSql = @"
                INSERT INTO oidcserver.refresh_token_family (
                  family_id, client_id, subject_id, op_sid, created_at
                ) VALUES (
                  @family_id, @client_id, @subject_id, @op_sid, NOW()
                )";

            await using (var cmd = new NpgsqlCommand(insertSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("family_id", NpgsqlDbType.Uuid, familyId);
                cmd.Parameters.AddWithValue("client_id", NpgsqlDbType.Text, clientId);
                cmd.Parameters.AddWithValue("subject_id", NpgsqlDbType.Text, subjectId);
                cmd.Parameters.AddWithValue("op_sid", NpgsqlDbType.Text, opSid);

                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return familyId;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> GetFamiliesByOpSidAsync(string opSid, CancellationToken ct)
        {
            var ids = new List<Guid>();
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
            SELECT family_id
            FROM oidcserver.refresh_token_family
            WHERE op_sid = @op_sid
              AND revoked_at IS NULL";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("op_sid", NpgsqlDbType.Text, opSid);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }

        /// <inheritdoc/>
        public async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Mark family revoked
            const string upfamily = @"
                UPDATE oidcserver.refresh_token_family
                SET revoked_at = NOW(), revoked_reason = @reason
                WHERE family_id = @family_id
                  AND revoked_at IS NULL";
            await using (var cmd = new NpgsqlCommand(upfamily, conn, tx))
            {
                cmd.Parameters.AddWithValue("family_id", NpgsqlDbType.Uuid, familyId);
                cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Text, (object?)reason ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Revoke all tokens in the family that are not already revoked/used
            const string uptokens = @"
            UPDATE oidcserver.refresh_token
            SET status = 'revoked', revoked_at = NOW(), revoked_reason = @reason
            WHERE family_id = @family_id
              AND status IN ('active','rotated')";
            await using (var cmd = new NpgsqlCommand(uptokens, conn, tx))
            {
                cmd.Parameters.AddWithValue("family_id", NpgsqlDbType.Uuid, familyId);
                cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Text, (object?)reason ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }

        /// <inheritdoc/>
        public async Task InsertAsync(RefreshTokenRow row, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                INSERT INTO oidcserver.refresh_token (
                    token_id, family_id, status,
                    lookup_key, hash, salt, iterations,
                    client_id, subject_id, external_id, subject_party_uuid, subject_party_id, subject_user_id, op_sid,
                    scopes, acr, amr, auth_time,
                    created_at, expires_at, absolute_expires_at,
                    rotated_to_token_id, revoked_at, revoked_reason,
                    user_agent_hash, ip_hash,
                    custom_claims
                ) VALUES (
                    @token_id, @family_id, @status,
                    @lookup_key, @hash, @salt, @iterations,
                    @client_id, @subject_id, @external_id, @subject_party_uuid, @subject_party_id, @subject_user_id, @op_sid,
                    @scopes, @acr, @amr, @auth_time,
                    @created_at, @expires_at, @absolute_expires_at,
                    @rotated_to_token_id, @revoked_at, @revoked_reason,
                    @user_agent_hash, @ip_hash,
                    @custom_claims
                )";

            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("token_id", NpgsqlDbType.Uuid, row.TokenId);
            cmd.Parameters.AddWithValue("family_id", NpgsqlDbType.Uuid, row.FamilyId);
            cmd.Parameters.AddWithValue("status", NpgsqlDbType.Text, row.Status);

            cmd.Parameters.AddWithValue("lookup_key", NpgsqlDbType.Bytea, row.LookupKey);
            cmd.Parameters.AddWithValue("hash", NpgsqlDbType.Bytea, row.Hash);
            cmd.Parameters.AddWithValue("salt", NpgsqlDbType.Bytea, row.Salt);
            cmd.Parameters.AddWithValue("iterations", NpgsqlDbType.Integer, row.Iterations);

            cmd.Parameters.AddWithValue("client_id", NpgsqlDbType.Text, row.ClientId);
            cmd.Parameters.AddWithValue("subject_id", NpgsqlDbType.Text, row.SubjectId);
            cmd.Parameters.AddWithValue("external_id", NpgsqlDbType.Text, (object?)row.ExternalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_uuid", NpgsqlDbType.Uuid, (object?)row.SubjectPartyUuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_id", NpgsqlDbType.Integer, (object?)row.SubjectPartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_user_id", NpgsqlDbType.Integer, (object?)row.SubjectUserId ?? DBNull.Value);

            cmd.Parameters.AddWithValue("op_sid", NpgsqlDbType.Text, row.OpSid);

            cmd.Parameters.AddWithValue("scopes", NpgsqlDbType.Array | NpgsqlDbType.Text, row.Scopes);
            cmd.Parameters.AddWithValue("acr", NpgsqlDbType.Text, (object?)row.Acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("amr", NpgsqlDbType.Array | NpgsqlDbType.Text, (object?)row.Amr ?? DBNull.Value);

            cmd.Parameters.AddWithValue("auth_time", NpgsqlDbType.TimestampTz, (object?)row.AuthTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_at", NpgsqlDbType.TimestampTz, row.CreatedAt);
            cmd.Parameters.AddWithValue("expires_at", NpgsqlDbType.TimestampTz, row.ExpiresAt);
            cmd.Parameters.AddWithValue("absolute_expires_at", NpgsqlDbType.TimestampTz, row.AbsoluteExpiresAt);

            cmd.Parameters.AddWithValue("rotated_to_token_id", NpgsqlDbType.Uuid, (object?)row.RotatedToTokenId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("revoked_at", NpgsqlDbType.TimestampTz, (object?)row.RevokedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("revoked_reason", NpgsqlDbType.Text, (object?)row.RevokedReason ?? DBNull.Value);

            cmd.Parameters.AddWithValue("user_agent_hash", NpgsqlDbType.Text, (object?)row.UserAgentHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ip_hash", NpgsqlDbType.Text, (object?)row.IpHash ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("custom_claims", row.CustomClaims));

            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<RefreshTokenRow?> GetByLookupKeyAsync(byte[] lookupKey, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                SELECT
                  token_id, family_id, status,
                  lookup_key, hash, salt, iterations,
                  client_id, subject_id, external_id, subject_party_uuid, subject_party_id, subject_user_id, op_sid,
                  scopes, acr, amr, auth_time,
                  created_at, expires_at, absolute_expires_at,
                  rotated_to_token_id, revoked_at, revoked_reason,
                  user_agent_hash, ip_hash,
                  custom_claims                    -- <--- NEW
                FROM oidcserver.refresh_token
                WHERE lookup_key = @lookup_key
                LIMIT 1";

            await using NpgsqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("lookup_key", NpgsqlDbType.Bytea, lookupKey);

            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return MapRow(reader);
        }

        /// <inheritdoc/>
        public async Task MarkUsedAsync(Guid tokenId, Guid rotatedToTokenId, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE oidcserver.refresh_token
                SET status = 'used',
                    rotated_to_token_id = @rotated_to,
                    revoked_at = NOW(),                -- optional: mark a terminal timestamp
                    revoked_reason = 'rotated'
                WHERE token_id = @token_id
                  AND status = 'active'";
            await using NpgsqlCommand cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("token_id", NpgsqlDbType.Uuid, tokenId);
            cmd.Parameters.AddWithValue("rotated_to", NpgsqlDbType.Uuid, rotatedToTokenId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task RevokeAsync(Guid tokenId, string reason, CancellationToken ct)
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE oidcserver.refresh_token
                SET status = 'revoked',
                    revoked_at = NOW(),
                    revoked_reason = @reason
                WHERE token_id = @token_id
                  AND status IN ('active','rotated')";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("token_id", NpgsqlDbType.Uuid, tokenId);
            cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Text, (object?)reason ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ---------------------------
        // Mapping
        // ---------------------------
        private static RefreshTokenRow MapRow(NpgsqlDataReader r)
        {
            // Column ordinals based on SELECT above
            var scopes = r["scopes"] is string[] s ? s : Array.Empty<string>();
            string[]? amr = null;
            if (r["amr"] is string[] amrArr)
            {
                amr = amrArr;
            }

            return new RefreshTokenRow
            {
                TokenId = r.GetGuid(r.GetOrdinal("token_id")),
                FamilyId = r.GetGuid(r.GetOrdinal("family_id")),
                Status = r.GetString(r.GetOrdinal("status")),
                LookupKey = (byte[])r["lookup_key"],
                Hash = (byte[])r["hash"],
                Salt = (byte[])r["salt"],
                Iterations = r.GetInt32(r.GetOrdinal("iterations")),
                ClientId = r.GetString(r.GetOrdinal("client_id")),
                SubjectId = r.GetString(r.GetOrdinal("subject_id")),
                ExternalId = r.IsDBNull("external_id") ? null : r.GetString("external_id"),
                SubjectPartyUuid = r.IsDBNull("subject_party_uuid") ? (Guid?)null : r.GetFieldValue<Guid>("subject_party_uuid"),
                SubjectPartyId = r.IsDBNull("subject_party_id") ? (int?)null : r.GetFieldValue<int>("subject_party_id"),
                SubjectUserId = r.IsDBNull("subject_user_id") ? (int?)null : r.GetFieldValue<int>("subject_user_id"),
                OpSid = r.GetString(r.GetOrdinal("op_sid")),
                Scopes = scopes,
                Acr = r.IsDBNull(r.GetOrdinal("acr")) ? null : r.GetString(r.GetOrdinal("acr")),
                Amr = amr,
                AuthTime = r.IsDBNull(r.GetOrdinal("auth_time")) ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("auth_time")),
                CreatedAt = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("created_at")),
                ExpiresAt = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("expires_at")),
                AbsoluteExpiresAt = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("absolute_expires_at")),
                RotatedToTokenId = r.IsDBNull(r.GetOrdinal("rotated_to_token_id")) ? null : r.GetGuid(r.GetOrdinal("rotated_to_token_id")),
                RevokedAt = r.IsDBNull(r.GetOrdinal("revoked_at")) ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("revoked_at")),
                RevokedReason = r.IsDBNull(r.GetOrdinal("revoked_reason")) ? null : r.GetString(r.GetOrdinal("revoked_reason")),
                UserAgentHash = r.IsDBNull(r.GetOrdinal("user_agent_hash")) ? null : r.GetString(r.GetOrdinal("user_agent_hash")),
                IpHash = r.IsDBNull(r.GetOrdinal("ip_hash")) ? null : r.GetString(r.GetOrdinal("ip_hash")),
                SessionId = r.GetString(r.GetOrdinal("op_sid")),
                CustomClaims = ReadDictJsonb(r, "custom_claims")
            };
        }

        private static NpgsqlParameter JsonbParam(string name, Dictionary<string, string>? dict)
        {
            return new NpgsqlParameter(name, NpgsqlDbType.Jsonb)
            {
                Value = dict is null ? DBNull.Value : JsonSerializer.Serialize(dict)
            };
        }

        private static Dictionary<string, string>? ReadDictJsonb(NpgsqlDataReader r, string col)
        {
            if (r.IsDBNull(col))
            {
                return null;
            }

            var json = r.GetFieldValue<string>(col); // jsonb → text
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
    }
}
