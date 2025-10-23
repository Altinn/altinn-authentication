using System.Data;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Constants.OidcServer;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// The repository implementation for <see cref="IUpstreamLoginTransactionRepository"/>.
    /// </summary>
    public sealed class UpstreamLoginTransactionRepository(
        NpgsqlDataSource dataSource,
        ILogger<UpstreamLoginTransactionRepository> logger,
        TimeProvider timeProvider) : IUpstreamLoginTransactionRepository
    {
        private readonly NpgsqlDataSource _ds = dataSource;
        private readonly ILogger<UpstreamLoginTransactionRepository> _logger = logger;
        private readonly TimeProvider _time = timeProvider;

        /// <inheritdoc/>
        public async Task<UpstreamLoginTransaction> InsertAsync(UpstreamLoginTransactionCreate create, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(create);
            bool hasDownstream = create.RequestId != Guid.Empty; 
            bool hasUnregistered = create.UnregisteredClientRequestId != Guid.Empty; 

            // Exactly one of hasDownstream or hasUnregistered must be true (not both, not neither)
            if (hasDownstream == hasUnregistered) 
            { 
                throw new ArgumentException("Exactly one of RequestId or UnregisteredClientRequestId must be set.", nameof(create)); 
            }

            if (string.IsNullOrWhiteSpace(create.Provider))
            {
                throw new ArgumentException("Provider required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.UpstreamClientId))
            {
                throw new ArgumentException("UpstreamClientId required.", nameof(create));
            }

            if (create.AuthorizationEndpoint is null || !create.AuthorizationEndpoint.IsAbsoluteUri)
            {
                throw new ArgumentException("AuthorizationEndpoint must be absolute.", nameof(create));
            }

            if (create.TokenEndpoint is null || !create.TokenEndpoint.IsAbsoluteUri)
            {
                throw new ArgumentException("TokenEndpoint must be absolute.", nameof(create));
            }

            if (create.UpstreamRedirectUri is null || !create.UpstreamRedirectUri.IsAbsoluteUri)
            {
                throw new ArgumentException("UpstreamRedirectUri must be absolute.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.State))
            {
                throw new ArgumentException("State required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.Nonce))
            {
                throw new ArgumentException("Nonce required.", nameof(create));
            }

            if (create.Scopes is null || create.Scopes.Length == 0)
            {
                throw new ArgumentException("Scopes required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.CodeVerifier))
            {
                throw new ArgumentException("CodeVerifier required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.CodeChallenge))
            {
                throw new ArgumentException("CodeChallenge required.", nameof(create));
            }

            DateTimeOffset now = _time.GetUtcNow();
            Guid upstreamRequestId = Guid.NewGuid();

            const string SQL = /*strpsql*/ $@"
                INSERT INTO {UpstreamLoginTransactionTable.TABLE} (
                    {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID},
                    {UpstreamLoginTransactionTable.REQUEST_ID},
                    {UpstreamLoginTransactionTable.UNREGISTERED_CLIENT_REQUEST_ID},
                    {UpstreamLoginTransactionTable.STATUS},
                    {UpstreamLoginTransactionTable.CREATED_AT},
                    {UpstreamLoginTransactionTable.EXPIRES_AT},

                    {UpstreamLoginTransactionTable.PROVIDER},
                    {UpstreamLoginTransactionTable.UPSTREAM_CLIENT_ID},
                    {UpstreamLoginTransactionTable.AUTHORIZATION_ENDPOINT},
                    {UpstreamLoginTransactionTable.TOKEN_ENDPOINT},
                    {UpstreamLoginTransactionTable.JWKS_URI},

                    {UpstreamLoginTransactionTable.UPSTREAM_REDIRECT_URI},

                    {UpstreamLoginTransactionTable.STATE},
                    {UpstreamLoginTransactionTable.NONCE},
                    {UpstreamLoginTransactionTable.SCOPES},
                    {UpstreamLoginTransactionTable.ACR_VALUES},
                    {UpstreamLoginTransactionTable.PROMPTS},
                    {UpstreamLoginTransactionTable.UI_LOCALES},
                    {UpstreamLoginTransactionTable.MAX_AGE},

                    {UpstreamLoginTransactionTable.CODE_VERIFIER},
                    {UpstreamLoginTransactionTable.CODE_CHALLENGE},
                    {UpstreamLoginTransactionTable.CODE_CHALLENGE_METHOD},

                    {UpstreamLoginTransactionTable.CORRELATION_ID},
                    {UpstreamLoginTransactionTable.CREATED_BY_IP},
                    {UpstreamLoginTransactionTable.USER_AGENT_HASH}
                )
                VALUES (
                    @upstream_request_id,
                    @request_id,
                    @unregistered_client_request_id,
                    'pending',
                    @created_at,
                    @expires_at,

                    @provider,
                    @upstream_client_id,
                    @authorization_endpoint,
                    @token_endpoint,
                  @jwks_uri,

                  @upstream_redirect_uri,

                  @state,
                  @nonce,
                  @scopes,
                  @acr_values,
                  @prompts,
                  @ui_locales,
                  @max_age,

                  @code_verifier,
                  @code_challenge,
                  @code_challenge_method,

                  @correlation_id,
                  @created_by_ip,
                  @user_agent_hash
                )
                RETURNING
                  {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID},
                  {UpstreamLoginTransactionTable.REQUEST_ID},
                  {UpstreamLoginTransactionTable.UNREGISTERED_CLIENT_REQUEST_ID},
                  {UpstreamLoginTransactionTable.STATUS},
                  {UpstreamLoginTransactionTable.CREATED_AT},
                  {UpstreamLoginTransactionTable.EXPIRES_AT},
                  {UpstreamLoginTransactionTable.COMPLETED_AT},

                  {UpstreamLoginTransactionTable.PROVIDER},
                  {UpstreamLoginTransactionTable.UPSTREAM_CLIENT_ID},
                  {UpstreamLoginTransactionTable.AUTHORIZATION_ENDPOINT},
                  {UpstreamLoginTransactionTable.TOKEN_ENDPOINT},
                  {UpstreamLoginTransactionTable.JWKS_URI},

                  {UpstreamLoginTransactionTable.UPSTREAM_REDIRECT_URI},

                  {UpstreamLoginTransactionTable.STATE},
                  {UpstreamLoginTransactionTable.NONCE},
                  {UpstreamLoginTransactionTable.SCOPES},
                  {UpstreamLoginTransactionTable.ACR_VALUES},
                  {UpstreamLoginTransactionTable.PROMPTS},
                  {UpstreamLoginTransactionTable.UI_LOCALES},
                  {UpstreamLoginTransactionTable.MAX_AGE},

                  {UpstreamLoginTransactionTable.CODE_VERIFIER},
                  {UpstreamLoginTransactionTable.CODE_CHALLENGE},
                  {UpstreamLoginTransactionTable.CODE_CHALLENGE_METHOD},

                  {UpstreamLoginTransactionTable.AUTH_CODE},
                  {UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT},
                  {UpstreamLoginTransactionTable.ERROR},
                  {UpstreamLoginTransactionTable.ERROR_DESCRIPTION},

                  {UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT},
                  {UpstreamLoginTransactionTable.UPSTREAM_ISSUER},
                  {UpstreamLoginTransactionTable.UPSTREAM_SUB},
                  {UpstreamLoginTransactionTable.UPSTREAM_ACR},
                  {UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME},
                  {UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI},
                  {UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID},

                  {UpstreamLoginTransactionTable.CORRELATION_ID},
                  {UpstreamLoginTransactionTable.CREATED_BY_IP},
                  {UpstreamLoginTransactionTable.USER_AGENT_HASH};";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);

                cmd.Parameters.AddWithValue("upstream_request_id", upstreamRequestId);
                cmd.Parameters.AddWithValue("request_id", DbNullIfEmpty(create.RequestId));
                cmd.Parameters.AddWithValue("unregistered_client_request_id", DbNullIfEmpty(create.UnregisteredClientRequestId));
                cmd.Parameters.AddWithValue("created_at", now);
                cmd.Parameters.AddWithValue("expires_at", create.ExpiresAt);

                cmd.Parameters.AddWithValue("provider", create.Provider);
                cmd.Parameters.AddWithValue("upstream_client_id", create.UpstreamClientId);
                cmd.Parameters.AddWithValue("authorization_endpoint", create.AuthorizationEndpoint.ToString());
                cmd.Parameters.AddWithValue("token_endpoint", create.TokenEndpoint.ToString());
                cmd.Parameters.AddWithValue("jwks_uri", (object?)create.JwksUri?.ToString() ?? DBNull.Value);

                cmd.Parameters.AddWithValue("upstream_redirect_uri", create.UpstreamRedirectUri.ToString());

                cmd.Parameters.AddWithValue("state", create.State);
                cmd.Parameters.AddWithValue("nonce", create.Nonce);

                cmd.Parameters.Add(new NpgsqlParameter<string[]>("scopes", create.Scopes) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                cmd.Parameters.Add(new NpgsqlParameter("acr_values", NpgsqlDbType.Array | NpgsqlDbType.Text)
                {
                    Value = (object?)create.AcrValues ?? DBNull.Value
                });
                cmd.Parameters.Add(new NpgsqlParameter("prompts", NpgsqlDbType.Array | NpgsqlDbType.Text)
                {
                    Value = (object?)create.Prompts ?? DBNull.Value
                });
                cmd.Parameters.Add(new NpgsqlParameter("ui_locales", NpgsqlDbType.Array | NpgsqlDbType.Text)
                {
                    Value = (object?)create.UiLocales ?? DBNull.Value
                });
                cmd.Parameters.AddWithValue("max_age", (object?)create.MaxAge ?? DBNull.Value);

                cmd.Parameters.AddWithValue("code_verifier", create.CodeVerifier);
                cmd.Parameters.AddWithValue("code_challenge", create.CodeChallenge);
                cmd.Parameters.AddWithValue("code_challenge_method", create.CodeChallengeMethod);

                cmd.Parameters.AddWithValue("correlation_id", (object?)create.CorrelationId ?? DBNull.Value);
                cmd.Parameters.Add(new NpgsqlParameter("created_by_ip", NpgsqlDbType.Inet) { Value = (object?)create.CreatedByIp ?? DBNull.Value });
                cmd.Parameters.AddWithValue("user_agent_hash", (object?)create.UserAgentHash ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new DataException("INSERT upstream_login_transaction returned no row.");
                }

                return Map(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // UpstreamLoginTransactionRepository // InsertAsync // request_id={RequestId}", create.RequestId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UpstreamLoginTransaction?> GetForCallbackByStateAsync(string upstreamState, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ $@"
                SELECT
                  {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID},
                  {UpstreamLoginTransactionTable.REQUEST_ID},
                  {UpstreamLoginTransactionTable.UNREGISTERED_CLIENT_REQUEST_ID},
                  {UpstreamLoginTransactionTable.STATUS},
                  {UpstreamLoginTransactionTable.CREATED_AT},
                  {UpstreamLoginTransactionTable.EXPIRES_AT},
                  {UpstreamLoginTransactionTable.COMPLETED_AT},
                  {UpstreamLoginTransactionTable.PROVIDER},
                  {UpstreamLoginTransactionTable.UPSTREAM_CLIENT_ID},
                  {UpstreamLoginTransactionTable.AUTHORIZATION_ENDPOINT},
                  {UpstreamLoginTransactionTable.TOKEN_ENDPOINT},
                  {UpstreamLoginTransactionTable.JWKS_URI},
                  {UpstreamLoginTransactionTable.UPSTREAM_REDIRECT_URI},
                  {UpstreamLoginTransactionTable.STATE},
                  {UpstreamLoginTransactionTable.NONCE},
                  {UpstreamLoginTransactionTable.SCOPES},
                  {UpstreamLoginTransactionTable.ACR_VALUES},
                  {UpstreamLoginTransactionTable.PROMPTS},
                  {UpstreamLoginTransactionTable.UI_LOCALES},
                  {UpstreamLoginTransactionTable.MAX_AGE},
                  {UpstreamLoginTransactionTable.CODE_VERIFIER},
                  {UpstreamLoginTransactionTable.CODE_CHALLENGE},
                  {UpstreamLoginTransactionTable.CODE_CHALLENGE_METHOD},
                  {UpstreamLoginTransactionTable.AUTH_CODE},
                  {UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT},
                  {UpstreamLoginTransactionTable.ERROR},
                  {UpstreamLoginTransactionTable.ERROR_DESCRIPTION},
                  {UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT},
                  {UpstreamLoginTransactionTable.UPSTREAM_ISSUER},
                  {UpstreamLoginTransactionTable.UPSTREAM_SUB},
                  {UpstreamLoginTransactionTable.UPSTREAM_ACR},
                  {UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME},
                  {UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI},
                  {UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID},
                  {UpstreamLoginTransactionTable.CORRELATION_ID},
                  {UpstreamLoginTransactionTable.CREATED_BY_IP},
                  {UpstreamLoginTransactionTable.USER_AGENT_HASH}
                FROM {UpstreamLoginTransactionTable.TABLE}
                WHERE {UpstreamLoginTransactionTable.STATE} = @state
                  AND {UpstreamLoginTransactionTable.STATUS} IN ('pending','callback_received')
                LIMIT 1;";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);
                cmd.Parameters.AddWithValue("state", upstreamState);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return Map(reader);
            }
            catch (Exception ex)
            {
                var sanitizedState = upstreamState.Replace("\r", string.Empty).Replace("\n", string.Empty);
                _logger.LogError(ex, "Authentication // UpstreamLoginTransactionRepository // GetForCallbackByStateAsync // state={State}", sanitizedState);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> SetCallbackSuccessAsync(Guid upstreamRequestId, string authCode, DateTimeOffset receivedAt, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ $@"
                UPDATE {UpstreamLoginTransactionTable.TABLE}
                SET {UpstreamLoginTransactionTable.AUTH_CODE} = @code,
                    {UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT} = @received_at,
                    {UpstreamLoginTransactionTable.STATUS} = 'callback_received'
                WHERE {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID} = @id
                  AND {UpstreamLoginTransactionTable.STATUS} = 'pending';";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("code", authCode);
            cmd.Parameters.AddWithValue("received_at", receivedAt);
            cmd.Parameters.AddWithValue("id", upstreamRequestId);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<int> SetCallbackErrorAsync(Guid upstreamRequestId, string error, string? errorDescription, DateTimeOffset receivedAt, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ $@"
        UPDATE {UpstreamLoginTransactionTable.TABLE}
        SET {UpstreamLoginTransactionTable.ERROR} = @error,
            {UpstreamLoginTransactionTable.ERROR_DESCRIPTION} = @desc,
            {UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT} = @received_at,
            {UpstreamLoginTransactionTable.STATUS} = 'error'
        WHERE {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID} = @id
            AND {UpstreamLoginTransactionTable.STATUS} IN ('pending','callback_received');";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("error", error);
            cmd.Parameters.AddWithValue("desc", (object?)errorDescription ?? DBNull.Value);
            cmd.Parameters.AddWithValue("received_at", receivedAt);
            cmd.Parameters.AddWithValue("id", upstreamRequestId);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<int> SetTokenExchangedAsync(
            Guid upstreamRequestId,
            string issuer,
            string sub,
            string? acr,
            DateTimeOffset? authTime,
            string? idTokenJti,
            string? sessionSid,
            DateTimeOffset exchangedAt,
            CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ $@"
            UPDATE {UpstreamLoginTransactionTable.TABLE}
            SET {UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT} = @exchanged_at,
                {UpstreamLoginTransactionTable.UPSTREAM_ISSUER} = @iss,
                {UpstreamLoginTransactionTable.UPSTREAM_SUB} = @sub,
                {UpstreamLoginTransactionTable.UPSTREAM_ACR} = @acr,
                {UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME} = @auth_time,
                {UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI} = @jti,
                {UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID} = @sid,
                {UpstreamLoginTransactionTable.STATUS} = 'token_exchanged'
            WHERE {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID} = @id
              AND {UpstreamLoginTransactionTable.STATUS} IN ('pending','callback_received');";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("exchanged_at", exchangedAt);
            cmd.Parameters.AddWithValue("iss", issuer);
            cmd.Parameters.AddWithValue("sub", sub);
            cmd.Parameters.AddWithValue("acr", (object?)acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_time", (object?)authTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("jti", (object?)idTokenJti ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sid", (object?)sessionSid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", upstreamRequestId);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<int> MarkCompletedAsync(Guid upstreamRequestId, bool success, DateTimeOffset completedAt, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ $@"
            UPDATE {UpstreamLoginTransactionTable.TABLE}
            SET {UpstreamLoginTransactionTable.STATUS} = @status,
                {UpstreamLoginTransactionTable.COMPLETED_AT} = @completed_at
            WHERE {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID} = @id
              AND {UpstreamLoginTransactionTable.STATUS} IN ('token_exchanged','error');";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("status", success ? "completed" : "cancelled");
            cmd.Parameters.AddWithValue("completed_at", completedAt);
            cmd.Parameters.AddWithValue("id", upstreamRequestId);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<int> MarkTokenExchangedAsync(
            Guid upstreamRequestId,
            string issuer,
            string sub,
            string? acr,
            DateTimeOffset? authTime,
            string? idTokenJti,
            string? upstreamSid,
            CancellationToken cancellationToken = default)
        {
            const string SQL = /*strpsql*/ $@"
            UPDATE {UpstreamLoginTransactionTable.TABLE}
            SET {UpstreamLoginTransactionTable.STATUS} = 'completed',
                {UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT} = @exchanged_at,
                {UpstreamLoginTransactionTable.UPSTREAM_ISSUER} = @issuer,
                {UpstreamLoginTransactionTable.UPSTREAM_SUB} = @sub,
                {UpstreamLoginTransactionTable.UPSTREAM_ACR} = @acr,
                {UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME} = @auth_time,
                {UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI} = @jti,
                {UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID} = COALESCE(@sid, {UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID})
            WHERE {UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID} = @id
            AND {UpstreamLoginTransactionTable.STATUS}
            IN('callback_received', 'token_exchanged')";

            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("issuer", issuer);
            cmd.Parameters.AddWithValue("exchanged_at", _time.GetUtcNow());
            cmd.Parameters.AddWithValue("sub", sub);
            cmd.Parameters.AddWithValue("acr", (object?)acr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_time", (object?)authTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("jti", (object?)idTokenJti ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sid", (object?)upstreamSid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", upstreamRequestId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // ---------- mapping ----------
        private static UpstreamLoginTransaction Map(NpgsqlDataReader r)
        {
            static Uri ToAbs(string s)
            {
                if (!Uri.TryCreate(s, UriKind.Absolute, out var u))
                {
                    throw new ArgumentException($"Expected absolute URI, got '{s}'.");
                }

                return u;
            }

            static Guid? GetGuidOrNull(NpgsqlDataReader rr, string col)
            {
                var ord = rr.GetOrdinal(col);
                return rr.IsDBNull(ord) ? (Guid?)null : rr.GetFieldValue<Guid>(ord);
            }

            static string[] GetArray(NpgsqlDataReader rr, string col) =>
                rr.IsDBNull(rr.GetOrdinal(col)) ? Array.Empty<string>() : rr.GetFieldValue<string[]>(col);

            static System.Net.IPAddress? GetIp(NpgsqlDataReader rr, string col)
            {
                var ord = rr.GetOrdinal(col);
                if (rr.IsDBNull(ord))
                {
                    return null;
                }

                try
                {
                    var inet = rr.GetFieldValue<NpgsqlTypes.NpgsqlInet>(ord);
                    return inet.Address;
                }
                catch
                {
                    return rr.GetFieldValue<System.Net.IPAddress>(ord);
                }
            }

            return new UpstreamLoginTransaction
            {
                UpstreamRequestId = r.GetFieldValue<Guid>(UpstreamLoginTransactionTable.UPSTREAM_REQUEST_ID),
                RequestId = GetGuidOrNull(r, UpstreamLoginTransactionTable.REQUEST_ID),
                UnregisteredClientRequestId = GetGuidOrNull(r, UpstreamLoginTransactionTable.UNREGISTERED_CLIENT_REQUEST_ID),
                Status = r.GetFieldValue<string>(UpstreamLoginTransactionTable.STATUS),
                CreatedAt = r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.CREATED_AT),
                ExpiresAt = r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.EXPIRES_AT),
                CompletedAt = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.COMPLETED_AT)) ? null : r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.COMPLETED_AT),

                Provider = r.GetFieldValue<string>(UpstreamLoginTransactionTable.PROVIDER),
                UpstreamClientId = r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_CLIENT_ID),
                AuthorizationEndpoint = ToAbs(r.GetFieldValue<string>(UpstreamLoginTransactionTable.AUTHORIZATION_ENDPOINT)),
                TokenEndpoint = ToAbs(r.GetFieldValue<string>(UpstreamLoginTransactionTable.TOKEN_ENDPOINT)),
                JwksUri = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.JWKS_URI)) ? null : ToAbs(r.GetFieldValue<string>(UpstreamLoginTransactionTable.JWKS_URI)),

                UpstreamRedirectUri = ToAbs(r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_REDIRECT_URI)),

                State = r.GetFieldValue<string>(UpstreamLoginTransactionTable.STATE),
                Nonce = r.GetFieldValue<string>(UpstreamLoginTransactionTable.NONCE),
                Scopes = GetArray(r, UpstreamLoginTransactionTable.SCOPES),
                AcrValues = GetArray(r, UpstreamLoginTransactionTable.ACR_VALUES),
                Prompts = GetArray(r, UpstreamLoginTransactionTable.PROMPTS),
                UiLocales = GetArray(r, UpstreamLoginTransactionTable.UI_LOCALES),
                MaxAge = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.MAX_AGE)) ? null : r.GetFieldValue<int>(UpstreamLoginTransactionTable.MAX_AGE),

                CodeVerifier = r.GetFieldValue<string>(UpstreamLoginTransactionTable.CODE_VERIFIER),
                CodeChallenge = r.GetFieldValue<string>(UpstreamLoginTransactionTable.CODE_CHALLENGE),
                CodeChallengeMethod = r.GetFieldValue<string>(UpstreamLoginTransactionTable.CODE_CHALLENGE_METHOD),

                AuthCode = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.AUTH_CODE)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.AUTH_CODE),
                AuthCodeReceivedAt = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT)) ? null : r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.AUTH_CODE_RECEIVED_AT),
                Error = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.ERROR)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.ERROR),
                ErrorDescription = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.ERROR_DESCRIPTION)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.ERROR_DESCRIPTION),

                TokenExchangedAt = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT)) ? null : r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.TOKEN_EXCHANGED_AT),
                UpstreamIssuer = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_ISSUER)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_ISSUER),
                UpstreamSub = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_SUB)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_SUB),
                UpstreamAcr = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_ACR)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_ACR),
                UpstreamAuthTime = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME)) ? null : r.GetFieldValue<DateTimeOffset>(UpstreamLoginTransactionTable.UPSTREAM_AUTH_TIME),
                UpstreamIdTokenJti = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_ID_TOKEN_JTI),
                UpstreamSessionSid = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.UPSTREAM_SESSION_SID),

                CorrelationId = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.CORRELATION_ID)) ? null : r.GetFieldValue<Guid>(UpstreamLoginTransactionTable.CORRELATION_ID),
                CreatedByIp = GetIp(r, UpstreamLoginTransactionTable.CREATED_BY_IP),
                UserAgentHash = r.IsDBNull(r.GetOrdinal(UpstreamLoginTransactionTable.USER_AGENT_HASH)) ? null : r.GetFieldValue<string>(UpstreamLoginTransactionTable.USER_AGENT_HASH)
            };
        }

        private static object DbNullIfEmpty(Guid value)
    => value == Guid.Empty ? DBNull.Value : value;
    }
}
