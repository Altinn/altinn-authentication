using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class ProfileMock : IProfile
    {
        public Task<UserProfile> GetUserProfile(UserProfileLookup profileLookup)
        {
            UserProfile userProfile = new UserProfile
            {
                UserId = profileLookup.UserId,
                UserUuid = Guid.NewGuid(),
                Party = new Party() { PartyId = profileLookup.UserId, SSN = "12345678901", Person = new Person() { SSN = "12345678901" } }
            };
            return Task.FromResult(userProfile);
        }
    }
}