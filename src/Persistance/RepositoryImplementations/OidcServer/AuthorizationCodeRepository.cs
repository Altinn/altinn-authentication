using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository implementation for authorization code management.
    /// </summary>
    public sealed class AuthorizationCodeRepository(NpgsqlDataSource ds, ILogger<AuthorizationCodeRepository> logger) : IAuthorizationCodeRepository
    {
        private readonly NpgsqlDataSource _ds = ds;
        private readonly ILogger<AuthorizationCodeRepository> _logger = logger;

        /// <inheritdoc/>
        public Task ConsumeAsync(string code, DateTimeOffset usedAt, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<AuthCodeRow?> GetAsync(string code, CancellationToken ct = default)
        {
            throw new NotImplementedException();
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
