using System.Data;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.OidcServer;

/// <summary>
/// Repository for reading migrated self-identified (SI) user credentials. See issue #2025.
/// </summary>
public sealed class SelfIdentifiedUserCredentialRepository(NpgsqlDataSource ds, ILogger<SelfIdentifiedUserCredentialRepository> logger) : ISelfIdentifiedUserCredentialRepository
{
    private readonly NpgsqlDataSource _ds = ds;
    private readonly ILogger<SelfIdentifiedUserCredentialRepository> _logger = logger;

    /// <inheritdoc/>
    public async Task<SelfIdentifiedUserCredential?> GetByUsernameAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }

        // Case-insensitive match mirrors Altinn 2 login behaviour. A lower(user_name) functional
        // index can be added if lookup volume warrants it.
        const string SQL = /*strpsql*/ @"
            SELECT party_uuid, user_id, user_name, password_hash, salt, password_expiry,
                   is_active, altinn2_user_id, imported_at
              FROM oidcserver.selfidentified_user_credential
             WHERE lower(user_name) = lower(@user_name)
             LIMIT 1;";

        await using var cmd = _ds.CreateCommand(SQL);
        cmd.Parameters.AddWithValue("user_name", userName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    private static SelfIdentifiedUserCredential Map(NpgsqlDataReader r)
    {
        return new SelfIdentifiedUserCredential
        {
            PartyUuid = r.GetFieldValue<Guid>("party_uuid"),
            UserId = r.GetFieldValue<int>("user_id"),
            UserName = r.GetFieldValue<string>("user_name"),
            PasswordHash = r.GetFieldValue<string>("password_hash"),
            Salt = r.GetFieldValue<string>("salt"),
            PasswordExpiry = r.GetFieldValue<DateTimeOffset>("password_expiry"),
            IsActive = r.GetFieldValue<bool>("is_active"),
            Altinn2UserId = r.IsDBNull("altinn2_user_id") ? null : r.GetFieldValue<int?>("altinn2_user_id"),
            ImportedAt = r.GetFieldValue<DateTimeOffset>("imported_at")
        };
    }
}
