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
                   email, is_active, altinn2_user_id, imported_at,
                   failed_login_attempts, lockout_until
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

    /// <inheritdoc/>
    public async Task RecordFailedAttemptAsync(string userName, int maxAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return;
        }

        // Increment the counter atomically. When the new counter value hits the threshold,
        // set lockout_until to NOW() + the lockout interval; otherwise leave it unchanged.
        // Using NOW() here ensures the database clock drives the lockout window, avoiding
        // any skew between the application server and the database.
        const string SQL = /*strpsql*/ @"
            UPDATE oidcserver.selfidentified_user_credential
               SET failed_login_attempts = failed_login_attempts + 1,
                   lockout_until = CASE
                       WHEN failed_login_attempts + 1 >= @max_attempts THEN NOW() + @lockout_interval
                       ELSE lockout_until
                   END
             WHERE lower(user_name) = lower(@user_name);";

        await using var cmd = _ds.CreateCommand(SQL);
        cmd.Parameters.AddWithValue("user_name", userName);
        cmd.Parameters.AddWithValue("max_attempts", maxAttempts);
        cmd.Parameters.AddWithValue("lockout_interval", lockoutDuration);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResetFailedAttemptsAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return;
        }

        const string SQL = /*strpsql*/ @"
            UPDATE oidcserver.selfidentified_user_credential
               SET failed_login_attempts = 0,
                   lockout_until = NULL
             WHERE lower(user_name) = lower(@user_name);";

        await using var cmd = _ds.CreateCommand(SQL);
        cmd.Parameters.AddWithValue("user_name", userName);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
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
            Email = r.IsDBNull("email") ? null : r.GetFieldValue<string>("email"),
            IsActive = r.GetFieldValue<bool>("is_active"),
            Altinn2UserId = r.IsDBNull("altinn2_user_id") ? null : r.GetFieldValue<int?>("altinn2_user_id"),
            ImportedAt = r.GetFieldValue<DateTimeOffset>("imported_at"),
            FailedLoginAttempts = r.GetFieldValue<int>("failed_login_attempts"),
            LockoutUntil = r.IsDBNull("lockout_until") ? null : r.GetFieldValue<DateTimeOffset?>("lockout_until"),
        };
    }
}
