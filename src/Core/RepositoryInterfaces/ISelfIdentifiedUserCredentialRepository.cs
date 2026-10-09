using Altinn.Platform.Authentication.Core.Models.Profile;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Reads self-identified (SI) user credentials migrated from Altinn 2, used to validate SI
    /// logins locally instead of via SBL Bridge. See issue #2025.
    /// </summary>
    public interface ISelfIdentifiedUserCredentialRepository
    {
        /// <summary>
        /// Gets the stored credential for the given username, or <c>null</c> if no such user exists.
        /// The match is case-insensitive to mirror Altinn 2 login behaviour. Returns the row
        /// regardless of <see cref="SelfIdentifiedUserCredential.IsActive"/> / expiry so the caller
        /// can apply policy and distinguish "no user" from "locked / expired".
        /// </summary>
        Task<SelfIdentifiedUserCredential?> GetByUsernameAsync(string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically increments <c>failed_login_attempts</c> for the given user. When the counter
        /// reaches <paramref name="maxAttempts"/>, <c>lockout_until</c> is set to
        /// <c>NOW() + <paramref name="lockoutDuration"/></c> in the database.
        /// Has no effect if the user does not exist.
        /// </summary>
        Task RecordFailedAttemptAsync(string userName, int maxAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets <c>failed_login_attempts</c> to 0 and clears <c>lockout_until</c> for the given
        /// user. Called after a successful authentication to clear any accumulated penalty.
        /// Has no effect if the user does not exist.
        /// </summary>
        Task ResetFailedAttemptsAsync(string userName, CancellationToken cancellationToken = default);
    }
}
