using System;
using System.Data;
using System.Data.Common;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Constants.OidcServer;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer
{
    /// <summary>
    /// Repository for OIDC server data
    /// </summary>
    public class OidcServerClientRepository(NpgsqlDataSource dataSource, ILogger<OidcServerClientRepository> logger, TimeProvider timeProvider) : IOidcServerRepository
    {
        private readonly NpgsqlDataSource _datasource = dataSource;
        private readonly ILogger<OidcServerClientRepository> _logger = logger;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc/>
        public async Task<OidcClient?> GetClientAsync(string clientId, CancellationToken ct = default)
        {
            const string QUERY = /*strpsql*/ @"SELECT
                        client_id,
                        client_name,
                        client_type,
                        token_endpoint_auth_method,
                        redirect_uris,
                        client_secret_hash,
                        client_secret_expires_at,
                        secret_rotation_at,
                        jwks_uri,
                        jwks,
                        allowed_scopes,
                        created_at,
                        updated_at
                    FROM client
                    WHERE client_id = @client_id
                    LIMIT 1;";

            try
            {
                await using var cmd = _datasource.CreateCommand(QUERY);
                cmd.Parameters.Add(new NpgsqlParameter<string>("client_id", clientId));

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return MapToOidcClient(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // OidcServerRepository // GetClientAsync // client_id={ClientId}", clientId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<OidcClient> InsertClientAsync(OidcClientCreate create, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(create);

            if (string.IsNullOrWhiteSpace(create.ClientId))
            {
                throw new ArgumentException("ClientId is required.", nameof(create));
            }

            if (string.IsNullOrWhiteSpace(create.ClientName))
            {
                throw new ArgumentException("ClientName is required.", nameof(create));
            }

            if (create.RedirectUris is null || create.RedirectUris.Count == 0)
            {
                throw new ArgumentException("At least one redirect URI is required.", nameof(create));
            }

            if (create.AllowedScopes is null || create.AllowedScopes.Count == 0)
            {
                throw new ArgumentException("At least one allowed scope is required.", nameof(create));
            }

            // Normalize inputs (defensive)
            var redirectUris = create.RedirectUris.Select(u =>
            {
                if (u is null || !u.IsAbsoluteUri)
                {
                    throw new ArgumentException($"Redirect URI must be absolute. Got: '{u}'.", nameof(create.RedirectUris));
                }

                return u.ToString();
            }).ToArray();

            var allowedScopes = create.AllowedScopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

            var now = _timeProvider.GetUtcNow();

            const string SQL = /*strpsql*/ @"
            INSERT INTO client (
                client_id,
                client_name,
                client_type,
                token_endpoint_auth_method,
                redirect_uris,
                client_secret_hash,
                client_secret_expires_at,
                secret_rotation_at,
                jwks_uri,
                jwks,
                allowed_scopes,
                created_at,
                updated_at
            )
            VALUES (
                @client_id,
                @client_name,
                @client_type,
                @token_endpoint_auth_method,
                @redirect_uris,
                @client_secret_hash,
                @client_secret_expires_at,
                @secret_rotation_at,
                @jwks_uri,
                @jwks,
                @allowed_scopes,
                @created_at,
                @updated_at
            )
            RETURNING
                client_id,
                client_name,
                client_type,
                token_endpoint_auth_method,
                redirect_uris,
                client_secret_hash,
                client_secret_expires_at,
                secret_rotation_at,
                jwks_uri,
                jwks,
                allowed_scopes,
                created_at,
                updated_at;";

            try
                {
                    await using var cmd = _datasource.CreateCommand(SQL);

                    // Required
                    cmd.Parameters.AddWithValue("client_id", create.ClientId);
                    cmd.Parameters.AddWithValue("client_name", create.ClientName);
                    cmd.Parameters.AddWithValue("client_type", create.ClientType.ToString()); // stored as TEXT
                    cmd.Parameters.AddWithValue("token_endpoint_auth_method", create.TokenEndpointAuthMethod.ToString()); // TEXT

                    // Arrays
                    var predirectUris = new NpgsqlParameter<string[]>("redirect_uris", redirectUris)
                    { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text };
                    cmd.Parameters.Add(predirectUris);

                    var pallowedScopes = new NpgsqlParameter<string[]>("allowed_scopes", allowedScopes)
                    { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text };
                    cmd.Parameters.Add(pallowedScopes);

                    // Optional secrets
                    cmd.Parameters.AddWithValue("client_secret_hash", (object?)create.ClientSecretHash ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("client_secret_expires_at", (object?)create.ClientSecretExpiresAt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("secret_rotation_at", (object?)create.SecretRotationAt ?? DBNull.Value);

                    // JWKS
                    cmd.Parameters.AddWithValue("jwks_uri", (object?)create.JwksUri?.ToString() ?? DBNull.Value);

                    var pjwks = new NpgsqlParameter("jwks", NpgsqlDbType.Jsonb)
                    {
                        Value = (object?)create.JwksJson ?? DBNull.Value
                    };
                    cmd.Parameters.Add(pjwks);

                    // Timestamps
                    cmd.Parameters.AddWithValue("created_at", now);
                    cmd.Parameters.AddWithValue("updated_at", DBNull.Value); // null at insert

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (!await reader.ReadAsync(ct))
                        {
                            throw new DataException("INSERT client returned no row.");
                        }

                        // Reuse your existing mapper that reads by column name constants
                    return MapToOidcClient(reader);
                }
                catch (PostgresException pex) when (pex.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    // Assuming client.client_id is UNIQUE/PK
                    _logger.LogWarning(pex, "Authentication // OidcServerRepository // InsertClientAsync // Unique violation for client_id={ClientId}", create.ClientId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Authentication // OidcServerRepository // InsertClientAsync // client_id={ClientId}", create.ClientId);
                    throw;
                }
            }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
            where TEnum : struct
            => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

        private static OidcClient MapToOidcClient(NpgsqlDataReader reader)
        {
            // Required
            string clientId = reader.GetFieldValue<string>(ClientTable.CLIENT_ID);
            string clientName = reader.GetFieldValue<string>(ClientTable.CLIENT_NAME);

            // Enums with fallback
            ClientType clientType = ParseEnum(
                reader.GetFieldValue<string>(ClientTable.CLIENT_TYPE),
                ClientType.Confidential);

            TokenEndpointAuthMethod authMethod = ParseEnum(
                reader.GetFieldValue<string>(ClientTable.TOKEN_ENDPOINT_AUTH_METHOD),
                TokenEndpointAuthMethod.ClientSecretBasic);

            // redirect_uris: read as TEXT[] then validate -> Uri[]
            string[] redirectUrisText = reader.GetFieldValue<string[]>(ClientTable.REDIRECT_URIS);
            Uri[] redirectUris = redirectUrisText
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s =>
                {
                    if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
                    {
                        throw new ArgumentException($"client.redirect_uris contains a non-absolute URI: '{s}'.");
                    }

                    return uri;
                })
                .ToArray();

            // allowed_scopes: TEXT[]
            string[] allowedScopes = reader.GetFieldValue<string[]>(ClientTable.ALLOWED_SCOPES)
                                           .Where(s => !string.IsNullOrWhiteSpace(s))
                                           .Select(s => s.Trim().ToLowerInvariant())
                                           .Distinct()
                                           .ToArray();

            // Optional secrets/keys (nullable)
            string? clientSecretHash = reader.IsDBNull(ClientTable.CLIENT_SECRET_HASH) ? null : reader.GetFieldValue<string>(ClientTable.CLIENT_SECRET_HASH);
            DateTimeOffset? clientSecretExp = reader.IsDBNull(ClientTable.CLIENT_SECRET_EXPIRES_AT) ? null : reader.GetFieldValue<DateTimeOffset>(ClientTable.CLIENT_SECRET_EXPIRES_AT);
            DateTimeOffset? secretRotationAt = reader.IsDBNull(ClientTable.SECRET_ROTATION_AT) ? null : reader.GetFieldValue<DateTimeOffset>(ClientTable.SECRET_ROTATION_AT);

            Uri? jwksUri = null;
            if (!reader.IsDBNull(ClientTable.JWKS_URI))
            {
                var s = reader.GetFieldValue<string>(ClientTable.JWKS_URI);
                if (!Uri.TryCreate(s, UriKind.Absolute, out jwksUri))
                {
                    throw new ArgumentException($"client.jwks_uri is not an absolute URI: '{s}'.");
                }
            }

            string? jwksJson = reader.IsDBNull(ClientTable.JWKS) ? null : reader.GetFieldValue<string>(ClientTable.JWKS);

            // Timestamps
            DateTimeOffset createdAt = reader.GetFieldValue<DateTimeOffset>(ClientTable.CREATED_AT);
            DateTimeOffset? updatedAt = reader.IsDBNull(ClientTable.UPDATED_AT) ? null : reader.GetFieldValue<DateTimeOffset>(ClientTable.UPDATED_AT);

            return new OidcClient(
                clientId: clientId,
                clientName: clientName,
                enabled: true, // map from column if/when added
                clientType: clientType,
                tokenEndpointAuthMethod: authMethod,
                redirectUris: redirectUris,
                allowedScopes: allowedScopes,
                requirePkce: true,                    // map from column if/when added
                allowedCodeChallengeMethods: new[] { "S256" },
                requireNonce: true,
                requireConsent: false,
                requireActorSelection: false,
                subjectType: SubjectType.Public,
                sectorIdentifierUri: null,
                pairwiseSalt: null,
                clientSecretHash: clientSecretHash,
                clientSecretExpiresAt: clientSecretExp,
                secretRotationAt: secretRotationAt,
                jwksUri: jwksUri,
                jwksJson: jwksJson,
                postLogoutRedirectUris: null,
                backchannelLogoutUri: null,
                frontchannelLogoutUri: null,
                allowTestIdp: false,
                requireParForTestIdp: true,
                createdAt: createdAt,
                updatedAt: updatedAt);
        }
    }
}
