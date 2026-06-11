#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Interface handling methods for operations related to users
    /// </summary>
    public interface IUserProfileService
    {
        /// <summary>
        /// Method that fetches a user based on ssn.
        /// </summary>
        /// <param name="ssnOrExternalIdentity">The user's ssn or external identity</param>
        /// <returns>User profile connected to given ssn or external identity</returns>
        Task<UserProfile> GetUser(string ssnOrExternalIdentity);

        /// <summary>
        /// Method that creates a new user
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns></returns>
        Task<UserProfile> CreateUser(UserProfile user);

        /// <summary>
        /// Endpoints that lookup a user based on username and password. This is used for validating credentials when using basic authentication.
        /// </summary>
        Task<UserCredentialVerificationResult> ValidateCredentialsAsync(string username, string password);

        /// <summary>
        /// Looks up the link-flow target for a self-identified user by username (issue #2035): the
        /// SI user's party UUID (the connection <c>from</c> party) and stored email. Returns
        /// <c>null</c> when the user is unknown, inactive, or has no email on file - so a single null
        /// check covers every "cannot proceed" case without revealing which one (no enumeration).
        /// </summary>
        /// <param name="username">The self-identified user's username (login key); may be null/empty.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<SelfIdentifiedLinkTarget?> GetSelfIdentifiedLinkTargetAsync(string? username, CancellationToken cancellationToken = default);
    }
}
