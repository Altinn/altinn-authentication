using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
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

                return MapClient(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // ClientRepository // GetClientAsync // client_id={ClientId}", clientId);
                throw;
            }
        }

        private static OidcClient MapClient(NpgsqlDataReader r)
        {
            return new OidcClient;
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
    }
}
