using System.Collections.Generic;
using System.Data;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

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
               AND expires_at > NOW();";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("code", code);
            cmd.Parameters.AddWithValue("client_id", clientId);
            cmd.Parameters.AddWithValue("redirect_uri", redirectUri.ToString());
            cmd.Parameters.AddWithValue("used_at", usedAt);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows == 1;
        }

        /// <inheritdoc/>
        /// <summary>
        /// Atomically marks the code as used (only if unused and unexpired) and returns the full row for token issuance.
        /// Returns null when the code is missing, already used, or expired.
        /// </summary>
        public async Task<AuthCodeRow?> GetAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("code is required", nameof(code));
            }

            // Single-shot atomic consume using UPDATE ... RETURNING.
            // If the WHERE predicate fails (used/expired/not found), zero rows are returned.
            const string SQL = /*strpsql*/ @"
                SELECT code, client_id, redirect_uri, code_challenge, code_challenge_method, used, expires_at,
                       subject_id, subject_party_uuid, subject_party_id, subject_user_id,
                       session_id, scopes, nonce, acr, auth_time
                FROM oidcserver.authorization_code
                WHERE code = @code
                  AND used = FALSE
                  AND expires_at > NOW()
                LIMIT 1;";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);
                cmd.Parameters.AddWithValue("code", code);
                cmd.Parameters.AddWithValue("used_at", _time.GetUtcNow());

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    // Not found, already used, or expired → caller should treat as invalid_grant
                    return null;
                }

                // Map the returned row to your domain model
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
                    SubjectPartyUuid = reader.IsDBNull("subject_party_uuid") ? (Guid?)null : reader.GetFieldValue<Guid>("subject_party_uuid"),
                    SubjectPartyId = reader.IsDBNull("subject_party_id") ? (int?)null : reader.GetFieldValue<int>("subject_party_id"),
                    SubjectUserId = reader.IsDBNull("subject_user_id") ? (int?)null : reader.GetFieldValue<int>("subject_user_id"),

                    SessionId = reader.GetFieldValue<string>("session_id"),

                    Scopes = reader.GetFieldValue<string[]>("scopes"),
                    Nonce = reader.IsDBNull("nonce") ? null : reader.GetFieldValue<string>("nonce"),
                    Acr = reader.IsDBNull("acr") ? null : reader.GetFieldValue<string>("acr"),
                    AuthTime = reader.IsDBNull("auth_time") ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>("auth_time")
                };

                return row;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Authentication // AuthorizationCodeRepository // GetAsync // Failed to consume code");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task InsertAsync(AuthorizationCodeCreate c, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ @"
            INSERT INTO oidcserver.authorization_code (
              code, client_id, subject_id, subject_party_uuid, subject_party_id, subject_user_id,
              session_id, redirect_uri, scopes, nonce, acr, auth_time,
              code_challenge, code_challenge_method, expires_at, created_by_ip, correlation_id
            )
            VALUES (
              @code, @client_id, @subject_id, @subject_party_uuid, @subject_party_id, @subject_user_id,
              @session_id, @redirect_uri, @scopes, @nonce, @acr, @auth_time,
              @code_challenge, @code_challenge_method, @expires_at, @created_by_ip, @correlation_id
            );";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("code", c.Code);
            cmd.Parameters.AddWithValue("client_id", c.ClientId);
            cmd.Parameters.AddWithValue("subject_id", c.SubjectId);
            cmd.Parameters.AddWithValue("subject_party_uuid", (object?)c.SubjectPartyUuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_party_id", (object?)c.SubjectPartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subject_user_id", (object?)c.SubjectUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("session_id", c.SessionId);
            cmd.Parameters.AddWithValue("redirect_uri", c.RedirectUri.ToString());
            cmd.Parameters.AddWithValue("scopes", c.Scopes.ToArray());
            cmd.Parameters.AddWithValue("nonce", (object?)c.Nonce ?? DBNull.Value);
            cmd.Parameters.AddWithValue("acr", (object?)c.Acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_time", (object?)c.AuthTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("code_challenge", c.CodeChallenge);
            cmd.Parameters.AddWithValue("code_challenge_method", c.CodeChallengeMethod);
            cmd.Parameters.AddWithValue("expires_at", c.ExpiresAt);
            cmd.Parameters.AddWithValue("created_by_ip", (object?)c.CreatedByIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("correlation_id", (object?)c.CorrelationId ?? DBNull.Value);

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
