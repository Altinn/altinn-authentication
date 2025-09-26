using System.Data;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Constants.OidcServer;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

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
            const string QUERY = /*strpsql*/ @"
            SELECT
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
