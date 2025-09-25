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
    public class OidcServerRepository(NpgsqlDataSource dataSource, ILogger<OidcServerRepository> logger, TimeProvider timeProvider) : IOidcServerRepository
    {
        private readonly NpgsqlDataSource _datasource = dataSource;
        private readonly ILogger<OidcServerRepository> _logger = logger;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc/>
        public async Task<OidcClient?> GetClientAsync(string clientId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("clientId is required", nameof(clientId));
            }

            const string QUERY = /*strpsql*/ @"
            SELECT
                client_id,
                client_name,
                client_type,
                token_endpoint_auth_method,
                redirect_uris,                -- TEXT[]
                client_secret_hash,
                client_secret_expires_at,     -- TIMESTAMPTZ NULL
                secret_rotation_at,           -- TIMESTAMPTZ NULL
                jwks_uri,                     -- TEXT NULL
                jwks,                         -- JSONB NULL
                allowed_scopes,               -- TEXT[] NOT NULL
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
                _logger.LogError(ex, "Authentication // ClientRepository // GetClientAsync // client_id={ClientId}", clientId);
                throw;
            }
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
            where TEnum : struct
            => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

        private static Uri ToAbsoluteUri(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Expected absolute URI, got '{value}'.");
            }

            return uri;
        }

        private static OidcClient MapToOidcClient(NpgsqlDataReader reader)
        {
            string clientId = reader.GetFieldValue<string>(ClientTable.CLIENT_ID);
            string clientName = reader.GetFieldValue<string>(ClientTable.CLIENT_NAME);

            ClientType clientType = ParseEnum(reader.GetFieldValue<string>(ClientTable.CLIENT_TYPE), ClientType.Confidential);
            TokenEndpointAuthMethod authMethod = ParseEnum(reader.GetFieldValue<string>(ClientTable.TOKEN_ENDPOINT_AUTH_METHOD), TokenEndpointAuthMethod.ClientSecretBasic);

            List<Uri> redirectUris = reader.GetFieldValue<List<Uri>>(ClientTable.REDIRECT_URIS);
            string[] allowedScopes = reader.GetFieldValue<string[]>(ClientTable.ALLOWED_SCOPES);

            string? clientSecretHash = reader.GetFieldValue<string?>(ClientTable.CLIENT_SECRET_HASH);
            DateTimeOffset clientSecretExpires = reader.GetFieldValue<DateTimeOffset>(ClientTable.CLIENT_SECRET_EXPIRES_AT);
            DateTimeOffset? secretRotationAt = reader.GetFieldValue<DateTimeOffset?>(ClientTable.SECRET_ROTATION_AT);

            Uri? jwksUri = reader.GetFieldValue<Uri?>(ClientTable.JWKS_URI);
            string? jwksJson = reader.GetFieldValue<string?>(ClientTable.JWKS);

            DateTimeOffset createdAt = reader.GetFieldValue<DateTimeOffset>(ClientTable.CREATED_AT);
            DateTimeOffset? updatedAt = reader.GetFieldValue<DateTimeOffset?>(ClientTable.UPDATED_AT);

            return new OidcClient(
                clientId: clientId,
                clientName: clientName,
                enabled: true, // map when you add an 'enabled' column
                clientType: clientType,
                tokenEndpointAuthMethod: authMethod,
                redirectUris: redirectUris,
                allowedScopes: allowedScopes,
                requirePkce: true, // or map from column when you add it
                allowedCodeChallengeMethods: new[] { "S256" },
                requireNonce: true,
                requireConsent: false,
                requireActorSelection: false,
                subjectType: SubjectType.Public,
                sectorIdentifierUri: null,
                pairwiseSalt: null,
                clientSecretHash: clientSecretHash,
                clientSecretExpiresAt: clientSecretExpires,
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
