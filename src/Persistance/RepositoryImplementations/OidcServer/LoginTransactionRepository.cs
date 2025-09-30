using System.Data;
using System.Net;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Constants.OidcServer;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository implementation for OIDC login transactions.
    /// </summary>
    public sealed class LoginTransactionRepository(
        NpgsqlDataSource dataSource,
        ILogger<LoginTransactionRepository> logger,
        TimeProvider timeProvider) : ILoginTransactionRepository
    {
        private readonly NpgsqlDataSource _ds = dataSource;
        private readonly ILogger<LoginTransactionRepository> _logger = logger;
        private readonly TimeProvider _time = timeProvider;

        /// <inheritdoc/>
        public async Task<LoginTransaction> InsertAsync(LoginTransactionCreate create, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(create);
            if (string.IsNullOrWhiteSpace(create.ClientId))
            {
                throw new ArgumentException("ClientId required.", nameof(create));
            }

            if (create.RedirectUri is null || !create.RedirectUri.IsAbsoluteUri)
            {
                throw new ArgumentException("RedirectUri must be absolute.", nameof(create));
            }

            if (create.Scopes is null || create.Scopes.Count == 0)
            {
                throw new ArgumentException("At least one scope required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.State))
            {
                throw new ArgumentException("State required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.Nonce))
            {
                throw new ArgumentException("Nonce required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.CodeChallenge))
            {
                throw new ArgumentException("CodeChallenge required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.CodeChallengeMethod))
            {
                throw new ArgumentException("CodeChallengeMethod required.", nameof(create));
            }

            // Normalize arrays as string[]
            var scopes = ToStringArray(create.Scopes);                 // required
            var acrValues = ToStringArrayOrNull(create.AcrValues);        // optional
            var prompts = ToStringArrayOrNull(create.Prompts);          // optional
            var uiLocales = ToStringArrayOrNull(create.UiLocales);        // optional

            var now = _time.GetUtcNow();

            const string SQL = /*strpsql*/ $@"
            INSERT INTO {LoginTransactionTable.TABLE} (
              {LoginTransactionTable.REQUEST_ID},
              {LoginTransactionTable.STATUS},
              {LoginTransactionTable.CREATED_AT},
              {LoginTransactionTable.EXPIRES_AT},
              {LoginTransactionTable.CLIENT_ID},
              {LoginTransactionTable.REDIRECT_URI},
              {LoginTransactionTable.SCOPES},
              {LoginTransactionTable.STATE},
              {LoginTransactionTable.NONCE},
              {LoginTransactionTable.ACR_VALUES},
              {LoginTransactionTable.PROMPTS},
              {LoginTransactionTable.UI_LOCALES},
              {LoginTransactionTable.MAX_AGE},
              {LoginTransactionTable.CODE_CHALLENGE},
              {LoginTransactionTable.CODE_CHALLENGE_METHOD},
              {LoginTransactionTable.REQUEST_URI},
              {LoginTransactionTable.REQUEST_OBJECT_JWT},
              {LoginTransactionTable.AUTHORIZATION_DETAILS},

              {LoginTransactionTable.CREATED_BY_IP},
              {LoginTransactionTable.USER_AGENT_HASH},
              {LoginTransactionTable.CORRELATION_ID}
            )
            VALUES (
              @request_id,
              'pending',
              @created_at,
              @expires_at,
              @client_id,
              @redirect_uri,
              @scopes,
              @state,
              @nonce,
              @acr_values,
              @prompts,
              @ui_locales,
              @max_age,
              @code_challenge,
              @code_challenge_method,
              @request_uri,
              @request_object_jwt,
              @authorization_details,
              @created_by_ip,
              @user_agent_hash,
              @correlation_id
            )
            RETURNING
              {LoginTransactionTable.REQUEST_ID},
              {LoginTransactionTable.STATUS},
              {LoginTransactionTable.CREATED_AT},
              {LoginTransactionTable.EXPIRES_AT},
              {LoginTransactionTable.COMPLETED_AT},
              {LoginTransactionTable.CLIENT_ID},
              {LoginTransactionTable.REDIRECT_URI},
              {LoginTransactionTable.SCOPES},
              {LoginTransactionTable.STATE},
              {LoginTransactionTable.NONCE},
              {LoginTransactionTable.ACR_VALUES},
              {LoginTransactionTable.PROMPTS},
              {LoginTransactionTable.UI_LOCALES},
              {LoginTransactionTable.MAX_AGE},
              {LoginTransactionTable.CODE_CHALLENGE},
              {LoginTransactionTable.CODE_CHALLENGE_METHOD},
              {LoginTransactionTable.REQUEST_URI},
              {LoginTransactionTable.REQUEST_OBJECT_JWT},
              {LoginTransactionTable.AUTHORIZATION_DETAILS},
              {LoginTransactionTable.CREATED_BY_IP},
              {LoginTransactionTable.USER_AGENT_HASH},
              {LoginTransactionTable.CORRELATION_ID},
              {LoginTransactionTable.UPSTREAM_REQUEST_ID};";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);

                // identity/lifecycle
                var requestId = Guid.CreateVersion7();
                cmd.Parameters.AddWithValue("request_id", requestId);
                cmd.Parameters.AddWithValue("created_at", now);
                cmd.Parameters.AddWithValue("expires_at", create.ExpiresAt);

                // client
                cmd.Parameters.AddWithValue("client_id", create.ClientId);
                cmd.Parameters.AddWithValue("redirect_uri", create.RedirectUri.ToString());

                // arrays
                cmd.Parameters.Add(new NpgsqlParameter<string[]>("scopes", scopes) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                cmd.Parameters.AddWithValue("state", create.State);
                cmd.Parameters.AddWithValue("nonce", create.Nonce);

                cmd.Parameters.Add(MakeTextArrayParam("scopes", scopes));
                cmd.Parameters.Add(MakeTextArrayParam("acr_values", acrValues));
                cmd.Parameters.Add(MakeTextArrayParam("prompts", prompts));
                cmd.Parameters.Add(MakeTextArrayParam("ui_locales", uiLocales));

                // primitives
                cmd.Parameters.AddWithValue("max_age", (object?)create.MaxAge ?? DBNull.Value);
                cmd.Parameters.AddWithValue("code_challenge", create.CodeChallenge);
                cmd.Parameters.AddWithValue("code_challenge_method", create.CodeChallengeMethod);

                // advanced
                cmd.Parameters.AddWithValue("request_uri", (object?)create.RequestUri ?? DBNull.Value);
                cmd.Parameters.AddWithValue("request_object_jwt", (object?)create.RequestObjectJwt ?? DBNull.Value);

                NpgsqlParameter authDetails = new("authorization_details", NpgsqlDbType.Jsonb)
                { Value = (object?)create.AuthorizationDetailsJson ?? DBNull.Value };
                cmd.Parameters.Add(authDetails);

                // diagnostics
                cmd.Parameters.AddWithValue("created_by_ip", (object?)create.CreatedByIp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("user_agent_hash", (object?)create.UserAgentHash ?? DBNull.Value);
                cmd.Parameters.AddWithValue("correlation_id", (object?)create.CorrelationId ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new DataException("INSERT login_transaction returned no row.");
                }

                return MapToLoginTransaction(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // LoginTransactionRepository // InsertAsync // client_id={ClientId}", create.ClientId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<LoginTransaction?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
        {
            const string SQL = /*strpsql*/ @"
                SELECT
                  request_id,
                  status,
                  created_at,
                  expires_at,
                  completed_at,

                  client_id,
                  redirect_uri,

                  scopes,
                  state,
                  nonce,
                  acr_values,
                  prompts,
                  ui_locales,
                  max_age,

                  code_challenge,
                  code_challenge_method,

                  request_uri,
                  request_object_jwt,
                  authorization_details,

                  created_by_ip,
                  user_agent_hash,
                  correlation_id,

                  upstream_request_id
                FROM oidcserver.login_transaction
                WHERE request_id = @request_id
                LIMIT 1;";

            try
            {
                await using var cmd = _ds.CreateCommand(SQL);
                cmd.Parameters.AddWithValue("request_id", requestId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return MapToLoginTransaction(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Authentication // LoginTransactionRepository // GetByRequestIdAsync // request_id={RequestId}",
                    requestId);
                throw;
            }
        }

        private static NpgsqlParameter MakeTextArrayParam(string name, string[]? value) =>
            new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

        private static string[] ToStringArray(IReadOnlyCollection<string> values) =>
            values is string[] arr ? arr
            : values.Select(s => s?.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)                // safe after filter
                    .ToArray();

        private static string[]? ToStringArrayOrNull(IReadOnlyCollection<string>? values) =>
            values is null ? null : ToStringArray(values);

        // ---------- mapping: constants only here ----------
        private static LoginTransaction MapToLoginTransaction(NpgsqlDataReader r)
        {
            string[] GetTextArray(string col) => r.IsDBNull(r.GetOrdinal(col)) ? Array.Empty<string>() : r.GetFieldValue<string[]>(col);
            string? GetNullableString(string col) => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetFieldValue<string>(col);

            Uri ToAbsoluteUri(string s)
            {
                if (!Uri.TryCreate(s, UriKind.Absolute, out var u))
                {
                    throw new ArgumentException($"Expected absolute URI, got '{s}'.");
                }

                return u;
            }

            IPAddress? GetIp(string col)
            {
                int ord = r.GetOrdinal(col);
                if (r.IsDBNull(ord))
                {
                    return null;
                }

                // Preferred: read as NpgsqlInet then take Address (handles IPv4/IPv6 and prefix length)
                try
                {
                    var inet = r.GetFieldValue<NpgsqlInet>(ord);
                    return inet.Address;
                }
                catch (InvalidCastException)
                {
                    // Fallback: some Npgsql versions also support IPAddress directly
                    return r.GetFieldValue<IPAddress>(ord);
                }
            }

            return new LoginTransaction
            {
                RequestId = r.GetFieldValue<Guid>(LoginTransactionTable.REQUEST_ID),
                Status = r.GetFieldValue<string>(LoginTransactionTable.STATUS),
                CreatedAt = r.GetFieldValue<DateTimeOffset>(LoginTransactionTable.CREATED_AT),
                ExpiresAt = r.GetFieldValue<DateTimeOffset>(LoginTransactionTable.EXPIRES_AT),
                CompletedAt = r.IsDBNull(r.GetOrdinal(LoginTransactionTable.COMPLETED_AT)) ? null : r.GetFieldValue<DateTimeOffset>(LoginTransactionTable.COMPLETED_AT),

                ClientId = r.GetFieldValue<string>(LoginTransactionTable.CLIENT_ID),
                RedirectUri = ToAbsoluteUri(r.GetFieldValue<string>(LoginTransactionTable.REDIRECT_URI)),

                Scopes = GetTextArray(LoginTransactionTable.SCOPES),
                State = r.GetFieldValue<string>(LoginTransactionTable.STATE),
                Nonce = GetNullableString(LoginTransactionTable.NONCE),
                AcrValues = GetTextArray(LoginTransactionTable.ACR_VALUES),
                Prompts = GetTextArray(LoginTransactionTable.PROMPTS),
                UiLocales = GetTextArray(LoginTransactionTable.UI_LOCALES),
                MaxAge = r.IsDBNull(r.GetOrdinal(LoginTransactionTable.MAX_AGE)) ? null : r.GetFieldValue<int>(LoginTransactionTable.MAX_AGE),

                CodeChallenge = r.GetFieldValue<string>(LoginTransactionTable.CODE_CHALLENGE),
                CodeChallengeMethod = r.GetFieldValue<string>(LoginTransactionTable.CODE_CHALLENGE_METHOD),

                RequestUri = GetNullableString(LoginTransactionTable.REQUEST_URI),
                RequestObjectJwt = GetNullableString(LoginTransactionTable.REQUEST_OBJECT_JWT),
                AuthorizationDetailsJson = r.IsDBNull(r.GetOrdinal(LoginTransactionTable.AUTHORIZATION_DETAILS)) ? null : r.GetFieldValue<string>(LoginTransactionTable.AUTHORIZATION_DETAILS),

                CreatedByIp = GetIp(LoginTransactionTable.CREATED_BY_IP),
                UserAgentHash = GetNullableString(LoginTransactionTable.USER_AGENT_HASH),
                CorrelationId = r.IsDBNull(r.GetOrdinal(LoginTransactionTable.CORRELATION_ID)) ? null : r.GetFieldValue<Guid>(LoginTransactionTable.CORRELATION_ID),

                UpstreamRequestId = r.IsDBNull(r.GetOrdinal(LoginTransactionTable.UPSTREAM_REQUEST_ID)) ? null : r.GetFieldValue<Guid>(LoginTransactionTable.UPSTREAM_REQUEST_ID),
            };
        }
    }
}
