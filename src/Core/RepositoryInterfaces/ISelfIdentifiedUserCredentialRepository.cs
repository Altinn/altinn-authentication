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
    }
}
