using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Model;

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
