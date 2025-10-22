#nullable enable
using System;
using System.Data;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Persistance.Constants.OidcServer;
using Docker.DotNet.Models;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Helpers
{
    public static class OidcServerDatabaseUtil
    {
        public static async Task<LoginTransaction?> GetDownstreamTransaction(string clientId, string state, NpgsqlDataSource DataSource, CancellationToken ct = default)
        {        
            const string SQL_FIND_DOWNSTREAM = /*strpsql*/ @"
            SELECT *
            FROM oidcserver.login_transaction
            WHERE client_id = @client_id AND state = @state
            LIMIT 1;";

            await using (var cmd = DataSource.CreateCommand(SQL_FIND_DOWNSTREAM))
            {
                cmd.Parameters.AddWithValue("client_id", clientId);
                cmd.Parameters.AddWithValue("state", state);

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return MapToLoginTransaction(reader);
            }
        }

        public static async Task<UpstreamLoginTransaction?> GetUpstreamTransaction(Guid requestId, NpgsqlDataSource DataSource, CancellationToken ct = default)
        {
            const string SQL_FIND_UPSTREAM = /*strpsql*/ @"
            SELECT * from oidcserver.login_transaction_upstream
            WHERE request_id = @request_id
            LIMIT 1;";

            await using (var cmd = DataSource.CreateCommand(SQL_FIND_UPSTREAM))
            {
                cmd.Parameters.AddWithValue("request_id", requestId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return MapUpstreamLoginTransaction(reader);
            }
        }

        public static async Task<UpstreamLoginTransaction?> GetUpstreamTransaction(string state, NpgsqlDataSource DataSource, CancellationToken ct = default)
        {
            const string SQL_FIND_UPSTREAM = /*strpsql*/ @"
            SELECT * from oidcserver.login_transaction_upstream
            WHERE state = @state
            LIMIT 1;";

            await using (var cmd = DataSource.CreateCommand(SQL_FIND_UPSTREAM))
            {
                cmd.Parameters.AddWithValue("state", state);

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return MapUpstreamLoginTransaction(reader);
            }
        }

        public static async Task<OidcSession?> GetOidcSessionAsync(string sid, NpgsqlDataSource _ds, CancellationToken ct = default)
        {
            const string SQL = "SELECT * FROM oidcserver.oidc_session WHERE sid=@sid LIMIT 1;";
            await using var cmd = _ds.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("sid", sid);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                return null;
            }

            return MapOidcSession(r);
        }

        private static OidcSession MapOidcSession(NpgsqlDataReader r)
        {
            return new OidcSession
            {
                Sid = r.GetFieldValue<string>("sid"),
                SessionHandle = r.GetFieldValue<byte[]>("session_handle_hash"),
                SubjectId = r.GetFieldValue<string>("subject_id"),
                ExternalId = r.IsDBNull("external_id") ? null : r.GetFieldValue<string>("external_id"),
                SubjectPartyUuid = r.IsDBNull("subject_party_uuid") ? null : r.GetFieldValue<Guid?>("subject_party_uuid"),
                SubjectPartyId = r.IsDBNull("subject_party_id") ? null : r.GetFieldValue<int?>("subject_party_id"),
                SubjectUserId = r.IsDBNull("subject_user_id") ? null : r.GetFieldValue<int?>("subject_user_id"),
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
            };
        }

        private static UpstreamLoginTransaction MapUpstreamLoginTransaction(NpgsqlDataReader r)
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
