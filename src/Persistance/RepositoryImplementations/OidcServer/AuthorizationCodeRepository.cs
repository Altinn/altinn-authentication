using System.Data;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository implementation for authorization code management.
    /// </summary>
    public sealed class AuthorizationCodeRepository(NpgsqlDataSource ds, ILogger<AuthorizationCodeRepository> logger, TimeProvider time) : IAuthorizationCodeRepository
    {
        private readonly NpgsqlDataSource _ds = ds;
        private readonly ILogger<AuthorizationCodeRepository> _logger = logger;
        private readonly TimeProvider _time = time;

        /// <inheritdoc/>
        public async Task<bool> TryConsumeAsync(string code, string clientId, Uri redirectUri, DateTimeOffset usedAt, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ @"
            UPDATE oidcserver.authorization_code
               SET used = TRUE, used_at = @used_at
             WHERE code = @code
               AND client_id = @client_id
               AND redirect_uri = @redirect_uri
               AND used = FALSE
               AND expires_at > @now;";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("code", NpgsqlDbType.Text, code);
            cmd.Parameters.AddWithValue("client_id", NpgsqlDbType.Text, clientId);
            cmd.Parameters.AddWithValue("redirect_uri", NpgsqlDbType.Text, redirectUri.ToString());
            cmd.Parameters.AddWithValue("used_at", NpgsqlDbType.TimestampTz, usedAt);
            cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, _time.GetUtcNow());

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows == 1;
        }

        /// <inheritdoc/>
        /// <summary>
        /// Returns the authorization code row if it exists, is unused, and unexpired at the current TimeProvider time.
        /// Caller performs consumption via <see cref="TryConsumeAsync"/>.
        /// </summary>
        public async Task<AuthCodeRow?> GetAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("code is required", nameof(code));
            }

            const string SQL = /*strpsql*/ @"
            SELECT code, client_id, redirect_uri, code_challenge, code_challenge_method, used, expires_at,
                    subject_id, external_id, subject_party_uuid, subject_party_id, subject_user_id,
                    session_id, scopes, nonce, acr, amr, auth_time
            FROM oidcserver.authorization_code
            WHERE code = @code
                AND used = FALSE
                AND expires_at > @now
            LIMIT 1;";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);
                cmd.Parameters.AddWithValue("code", NpgsqlDbType.Text, code);
                cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, _time.GetUtcNow());

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    // Not found, already used, or expired → treat as invalid_grant
                    return null;
                }

                var row = new AuthCodeRow
                {
                    Code = reader.GetFieldValue<string>("code"),
                    ClientId = reader.GetFieldValue<string>("client_id"),
                    RedirectUri = new Uri(reader.GetFieldValue<string>("redirect_uri"), UriKind.Absolute),

                    CodeChallenge = reader.GetFieldValue<string>("code_challenge"),
                    CodeChallengeMethod = reader.GetFieldValue<string>("code_challenge_method"),

                    Used = reader.GetFieldValue<bool>("used"),
                    ExpiresAt = reader.GetFieldValue<DateTimeOffset>("expires_at"),

                    SubjectId = reader.GetFieldValue<string>("subject_id"),
                    ExternalId = reader.IsDBNull("external_id") ? null : reader.GetFieldValue<string>("external_id"),
                    SubjectPartyUuid = reader.IsDBNull("subject_party_uuid") ? (Guid?)null : reader.GetFieldValue<Guid>("subject_party_uuid"),
                    SubjectPartyId = reader.IsDBNull("subject_party_id") ? (int?)null : reader.GetFieldValue<int>("subject_party_id"),
                    SubjectUserId = reader.IsDBNull("subject_user_id") ? (int?)null : reader.GetFieldValue<int>("subject_user_id"),

                    SessionId = reader.GetFieldValue<string>("session_id"),

                    Scopes = reader.GetFieldValue<string[]>("scopes"),
                    Nonce = reader.IsDBNull("nonce") ? null : reader.GetFieldValue<string>("nonce"),
                    Acr = reader.IsDBNull("acr") ? null : reader.GetFieldValue<string>("acr"),
                    Amr = reader.IsDBNull("amr") ? null : reader.GetFieldValue<string[]>("amr"),
                    AuthTime = reader.IsDBNull("auth_time") ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>("auth_time")
                };

                return row;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthorizationCodeRepository.GetAsync failed for code {Code}", code);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task InsertAsync(AuthorizationCodeCreate c, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ @"
            INSERT INTO oidcserver.authorization_code (
              code, client_id, subject_id, external_id, subject_party_uuid, subject_party_id, subject_user_id,
              session_id, redirect_uri, scopes, nonce, acr, amr, auth_time,
              code_challenge, code_challenge_method, issued_at, expires_at, created_by_ip, correlation_id
            )
            VALUES (
              @code, @client_id, @subject_id, @external_id, @subject_party_uuid, @subject_party_id, @subject_user_id,
              @session_id, @redirect_uri, @scopes, @nonce, @acr, @amr, @auth_time,
              @code_challenge, @code_challenge_method, @issued_at, @expires_at, @created_by_ip, @correlation_id
            );";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("code", NpgsqlDbType.Text, c.Code);
            cmd.Parameters.AddWithValue("client_id", NpgsqlDbType.Text, c.ClientId);
            cmd.Parameters.AddWithValue("subject_id", NpgsqlDbType.Text, c.SubjectId);
            cmd.Parameters.AddWithValue("external_id", NpgsqlDbType.Text, (object?)c.ExternalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_uuid", NpgsqlDbType.Uuid, (object?)c.SubjectPartyUuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_id", NpgsqlDbType.Integer, (object?)c.SubjectPartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_user_id", NpgsqlDbType.Integer, (object?)c.SubjectUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("session_id", NpgsqlDbType.Text, c.SessionId);
            cmd.Parameters.AddWithValue("redirect_uri", NpgsqlDbType.Text, c.RedirectUri.ToString());
            cmd.Parameters.AddWithValue("scopes", NpgsqlDbType.Array | NpgsqlDbType.Text, c.Scopes.ToArray());
            cmd.Parameters.AddWithValue("nonce", NpgsqlDbType.Text, (object?)c.Nonce ?? DBNull.Value);
            cmd.Parameters.AddWithValue("acr", NpgsqlDbType.Text, (object?)c.Acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("amr", NpgsqlDbType.Array | NpgsqlDbType.Text, (object?)c.Amr?.ToArray() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_time", NpgsqlDbType.TimestampTz, (object?)c.AuthTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("code_challenge", NpgsqlDbType.Text, c.CodeChallenge);
            cmd.Parameters.AddWithValue("code_challenge_method", NpgsqlDbType.Text, c.CodeChallengeMethod);
            cmd.Parameters.AddWithValue("issued_at", NpgsqlDbType.TimestampTz, c.IssuedAt);
            cmd.Parameters.AddWithValue("expires_at", NpgsqlDbType.TimestampTz, c.ExpiresAt);
            cmd.Parameters.AddWithValue("created_by_ip", NpgsqlDbType.Inet, (object?)c.CreatedByIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("correlation_id", NpgsqlDbType.Uuid, (object?)c.CorrelationId ?? DBNull.Value);

            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthorizationCodeRepository.InsertAsync failed for client_id={ClientId}", c.ClientId);
                throw;
            }
        }
    }
}
