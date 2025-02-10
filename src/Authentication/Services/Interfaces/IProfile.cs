using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Profile.Models;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Altinn Profile Interface
    /// </summary>
    public interface IProfile
    {
        /// <summary>
        /// Returns the user profile for a given user
        /// </summary>
        Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup);
    }
}
