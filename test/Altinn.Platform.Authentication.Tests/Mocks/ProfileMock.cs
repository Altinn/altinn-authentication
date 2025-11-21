using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class ProfileMock : IProfile
    {
        public Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup)
        {
            UserProfile userProfile = new UserProfile();
            userProfile.UserId = profileLookup.UserId;
            userProfile.UserUuid = Guid.NewGuid();
            return Task.FromResult(userProfile);
        }
    }
}