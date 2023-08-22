using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Defines event log interface to queue an authentication event to a storage queue
    /// </summary>
    public interface IEventLog
    {
        /// <summary>
        /// Creates an authentication event in storage queue
        /// </summary>
        /// <param name="authenticationEvent">authentication event</param>
        public void CreateAuthenticationEvent(AuthenticationEvent authenticationEvent);

        /// <summary>
        /// Creates an authentication event in storage queue
        /// </summary>
        /// <param name="authenticatedUser">authenticated user information</param>
        public void CreateAuthenticationEvent(UserAuthenticationModel authenticatedUser);
    }
}
